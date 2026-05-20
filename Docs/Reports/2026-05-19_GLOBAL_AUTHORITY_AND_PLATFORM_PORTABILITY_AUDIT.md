# Global Authority And Platform Portability Audit

Date: 2026-05-19
Status: PENDING VERIFICATION
Agent: HFI_AUDIT
Evidence class: STATIC_SOURCE / STATIC_DOC / PROJECT_SETTINGS / PACKAGE_SETTINGS / FILESYSTEM / OFFICIAL_PLATFORM_DOC

No Unity import, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger,
player build, Android/Quest/PICO build, Steam Deck run, macOS Metal run, or
console SDK validation was executed in this pass.

## Executive Verdict

The direction is globally correct, but not yet proven.

The project is building the right kind of runtime government for HECTON-8:

- `GlobalRegistry` as cold service identity and dependency injection source.
- `SystemDispatcher` as phase and cadence authority.
- `SignalBus<T>` as typed first-party broadcast traffic.
- `GlobalSignals` as a legacy bridge and typed-lane initializer.
- `HectonEventBus` as mod/API/cold boundary.
- `GlobalDataVault` as cross-domain native state, snapshot, generation, and
  crash/postmortem surface.
- `GlobalQualityWeight` as continuous platform pressure scalar.

The risk is also obvious: the surface is huge. If agents keep adding global
routes as convenience plumbing, these systems become four global god objects:
registry, event bus, signal bridge, and data heap.

Current state is a controlled warning, not a collapse.

2026-05-19 R8 current delta: Quest/OpenXR package bootstrap has started.
`Packages/manifest.json` now contains XR Management, OpenXR, and Unity Meta
OpenXR. Android package id, explicit target SDK, custom manifest, and custom
Gradle template are now set. Unity has not imported/resolved those packages in
`packages-lock.json`, and XR Plug-in Management provider settings are still not
generated. Therefore Quest/PCVR status improves from "package-blocked" to
"provider/settings/build-proof blocked".

## Static Snapshot

Broad source-only grep orientation under `Assets/_Project/Scripts`:

| Signal | Count |
|---|---:|
| `GlobalRegistry.` dot hits | 6147 |
| `GlobalRegistry.Get<T>` hits | 0 |
| `HectonEventBus.Publish/Subscribe` hits | 48 |
| `GlobalSignals.Publish` hits | 259 |
| `SignalBus<...>` refs | 1303 |
| Direct `.Push(...)` / `.TryPush(...)` text hits | 302 |
| `GlobalDataVault` / `IDataVault` / Vault handle refs | 4752 |
| Native collection refs | 15468 |
| XR/OpenXR refs | 127 |
| Android/Quest/PICO refs | 2836 |
| Mac/Metal refs | 2606 |
| Linux/Steam Deck refs | 217 |
| Console regex refs in first-party scripts | 0 |

These are static text counters only. They are useful for pressure detection, not
runtime health.

## Global Authority Assessment

### GlobalRegistry

Evidence anchors:

- `Assets/_Project/Scripts/Core/GlobalRegistry.cs:74` defines the static
  registry.
- `GlobalRegistry.cs:79` defines registry phase state.
- `GlobalRegistry.cs:742` exposes `IDataVault`.
- `GlobalRegistry.cs:1854` exposes `SystemDispatcher`.
- `GlobalRegistry.cs:4003` registers the DataVault.
- `GlobalRegistry.cs:5796` and later routes tick registration through the
  dispatcher.

Verdict: direction is correct, surface is high-risk yellow.

The positive part: no `GlobalRegistry.Get<T>` hits were found by the current
regex. The project is mostly using explicit slots/properties and registration
methods instead of generic service-location calls.

The negative part: 6137 raw `GlobalRegistry.` hits and a 6676-line registry file
mean the registry is close to becoming a concrete domain catalog. The registry
must remain cold identity. Hot systems must bind once and cache dependencies.

Required rule: no new registry slot unless it replaces older debt, removes scene
search/singleton behavior, or is required by a documented route card.

### SignalBus And GlobalSignals

Evidence anchors:

- `Assets/_Project/Scripts/Core/GlobalSignals.cs:1556` defines `SignalBus<T>`.
- `GlobalSignals.cs:1647` configures lane capacity.
- `GlobalSignals.cs:1657` initializes typed lanes.
- `GlobalSignals.cs:1667` allocates the persistent `NativeQueue<T>` lane.
- `GlobalSignals.cs:4914` defines the `GlobalSignals` bridge class.
- `GlobalSignals.cs:5909` and later publish wrappers route legacy calls into
  typed `SignalBus<T>.Push`.

Verdict: correct technical direction, migration still incomplete.

Typed lanes, unmanaged layouts, explicit capacities, low-tier frame limits, and
snapshot reads are the right model for first-party hot broadcasts. This is one
of the strongest architectural moves in the project.

The danger is that `GlobalSignals.Publish` still has 259 current call sites and
the file is a large mixed surface. That is acceptable only as a bridge. New
first-party traffic should be direct typed `SignalBus<T>` or owner-interface
traffic, not more generic bridge usage.

Signal payload sizes also need pressure review. Many payloads are explicit 32 or
64 bytes, but 80/96/128/144/160-byte payloads exist. Some can be justified;
none should enter high-frequency lanes without telemetry and coalescing.

### HectonEventBus

Evidence anchors:

- Top current call sites are mostly mod/meta/cold: `ModdingAPI/HectonAPI.cs`,
  `ModCommandDispatcher.cs`, `Meta/GlobalProfileManager.cs`, and
  `Meta/DynamicDifficultyDirector.cs`.
- Runtime-adjacent call sites still exist in progression, economy, UI, inventory,
  and environmental strain.

Verdict: acceptable only if classified. Red if used as first-party hot bus.

Current count is 52 publish/subscribe hits. This is not automatically bad. It is
bad only when a first-party runtime system uses it to avoid a typed SignalBus
lane or direct owner interface.

Required action: classify every call as `MOD_API_COLD`, `FIRST_PARTY_COLD`, or
`FIRST_PARTY_HOT`. `FIRST_PARTY_HOT` must migrate.

### GlobalDataVault

Evidence anchors:

- `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:27` defines
  `IDataVault`.
- `GlobalDataVault.cs:105` and `:108` expose buffer and handle acquisition.
- `GlobalDataVault.cs:127` resolves generation-checked handles.
- `GlobalDataVault.cs:136` releases owner buffers.
- `GlobalDataVault.cs:149` and `:152` define lock paths.
- `GlobalDataVault.cs:446` defines the concrete vault.
- `GlobalDataVault.cs:1651` runs defrag with phase/stress/lock mask.
- `GlobalDataVault.cs:2599` and `:2639` record/dump defrag black-box state.

Verdict: strong core design, dangerous ownership hygiene.

The vault itself has the right primitives: `BufferID`, `SystemID`, generation
handles, stale-handle checks, locks, owner release, defrag telemetry, and binary
dump capability.

The project-wide danger is not the Vault class. The danger is uncontrolled
BufferID ownership and hard-cast ranges. Earlier HFI logs already found
collisions and local hard-cast ranges. On a global native heap, numeric aliasing
is not a small bug. It is cross-system corruption.

Required rule: all new Vault buffers must go through a central `BufferID` ledger
or enum path with owner, range, length, lifecycle, and stale-handle behavior.
Local scratch must stay local.

Current HFI BufferID sovereignty audit adds a hard gate: `python
Tools/BufferIDSovereigntyAudit.py --fail-on-duplicates`. R12 repaired the
central duplicate by moving `ConstructionBuilderOccupancy` from `70200` to
`70319`, leaving `SaveWorldPagerWriteArena` at `70200`. Latest static result:
0 duplicate central values; 604 local numeric casts outside `H8Memory.cs` across
50 files. This is partial DataVault governance repair, not a reason to remove
the vault.

### SystemDispatcher And Phase Model

Verdict: direction is correct, runtime proof absent.

The project is moving toward phase-owned execution instead of random Unity loop
methods. This is required for deterministic signals, job completion windows,
and platform throttling.

Risk: hidden `.Complete()` or queue drain stalls can still exist. Static phase
order does not prove frame stability.

Required proof: profiler capture with dispatcher markers under signal/DataVault
stress.

## Platform Portability Assessment

### Windows PC, Variable Power

Static foundation: strongest target.

Current positives:

- URP 17.4.0 is present in `Packages/manifest.json:12`.
- Addressables, Input System, and Memory Profiler packages are present.
- Burst AOT settings exist for Windows and Android.
- Quality tiers exist: `Surface (Medium)`, `Abyss (Low)`, and `Orbit (High)`.
- Streaming mipmaps are active.
- The scalability docs and runtime surfaces use continuous quality pressure.

Current blockers:

- No fresh Unity import/player build/profiler proof in this pass.
- No proven first-20-minutes route capture.
- Large third-party physical surface remains under `Assets`, including heavy
  packages and Windows-only native plugin risk.
- Content payload authority is incomplete.

Verdict: Windows flat PC is the correct first proof platform. It is not proven
ready; it is the best place to prove the Copper Wire vertical route first.

### Low-End PC / MX350 Class

Static foundation: meaningful, not proven.

The project has explicit MX350-era budgets and scalability docs. That is good.
`ThermalDynamicResolutionAdapter` reads `GlobalQualityWeight`, writes shader
globals, uses dynamic resolution, consumes thermal/foveated pressure signals,
and estimates visual budget.

But low-tier success cannot be inferred from code shape. The asset/vendor/shader
stack is heavy. The route must be captured on the target class or an honest
proxy with frame time, GC, memory, VRAM, SetPass, and hitch notes.

Verdict: direction correct. Proof absent.

### High-End PC

Static foundation: good for overkill scaling, not content proof.

The architecture supports spending saved cycles on visual overkill through
continuous `GlobalQualityWeight`, render-scale adaptation, SignalBus fan-out,
and visual sync consumers. That is the right idea.

The risk is permanent base bloat. High-end visuals must be additive after the
low baseline is stable, not baked into the mandatory path.

Verdict: correct model. Needs a low baseline first.

### PCVR

Static foundation: partial, currently blocked by provider/settings/proof.

Current positives:

- `FoveatedRenderCommander` exists and is not fake. It enumerates
  `XRDisplaySubsystem`, reads `SystemInfo.foveatedRenderingCaps`, tracks gaze
  capability, has Quest classification, DataVault telemetry, and black-box dump
  logic.
- `OpenXRManualOverrideLever` and XR runtime state/input surfaces exist.
- `Packages/manifest.json` now contains `com.unity.xr.management`,
  `com.unity.xr.openxr`, and `com.unity.xr.meta-openxr`.

Hard blockers:

- Unity package resolve/import has not run; `packages-lock.json` still does not
  prove the XR packages were imported.
- `ProjectSettings/ProjectSettings.asset:544` has
  `m_BuildTargetVRSettings: []`.
- OpenXR loader/provider assets are not generated for Standalone or Android.
- No headset runtime smoke test exists.

Verdict: PCVR direction is right, but current project configuration blocks real
PCVR readiness claims.

### Standalone Quest 2 / Quest 3

Static foundation: early red/yellow.

Current positives:

- Android IL2CPP is configured in `ProjectSettings.asset:859`.
- Android managed stripping is present at `ProjectSettings.asset:864`.
- Android target architecture is `2` at `ProjectSettings.asset:270`, consistent
  with the ARM64-only direction required for modern standalone Android targets.
- Android graphics API is explicit, not automatic, at `ProjectSettings.asset:534`.
- `Packages/manifest.json` now contains XR Management, OpenXR, and Unity Meta
  OpenXR package ids.
- `ProjectSettings.asset:169` now uses `com.danatgames.hecton8`, not the Unity
  template Android package id.
- `ProjectSettings.asset:180` now uses explicit Android target SDK `35`.
- `ProjectSettings.asset:262` and `:264` now enable the custom Android manifest
  and custom main Gradle template.
- Quest-specific runtime policy and foveation code exist.
- `XrPlatformReadinessValidator` now checks missing XR packages, manifest
  markers, template app id, automatic target SDK, custom manifest, custom Gradle,
  and ARM64-only architecture.

Hard blockers:

- Unity package resolve/import has not run; `packages-lock.json` has no XR
  package lock entries yet.
- Empty `m_BuildTargetVRSettings`.
- XR Plug-in Management Android loader settings are not generated.
- OpenXR/Meta feature groups are not enabled/proven in Project Settings.
- Android VR manifest requirements are statically present but not device-proven.
- No Quest 2/Quest 3 IL2CPP build, install, launch, thermal, or comfort proof.

Unity's current XR docs require XR Plug-in Management to enable/configure target
XR plugins. Unity's Meta Quest docs state Unity supports Quest 2/3/3S/Pro
development and exposes Meta/OpenXR package routes. Unity's URP untethered-XR
guidance also pushes Vulkan, OpenXR, foveated rendering, and mobile tile-GPU
render discipline. The project has some of the code for this, but not the
provider/settings/device proof.

Verdict: not ready. Direction is better than the previous snapshot because the
package/settings bootstrap has started. The next blocker is Unity-side XR loader
configuration plus a separate standalone-XR rendering profile.

### PICO Standalone VR

Static foundation: similar to Quest but less integrated.

PICO documentation expects its SDK/provider path under XR Plug-in Management,
including a PXR manager/SDK workflow for device packaging. Current project
settings do not show PICO XR provider setup, and no PICO package is present in
the manifest.

OpenXR can reduce abstraction cost, but PICO is not "free Quest support." Input,
haptics, foveation, store services, entitlement, and device runtime behavior
need separate proof.

Verdict: possible later. Currently not configured.

### Steam Deck / Linux

Static foundation: plausible, not proven.

Current positives:

- Linux/Steam Deck references exist in source/docs.
- Steam Deck is a sensible target for low/mid scalability pressure: 1280x800,
  gamepad-first controls, limited APU power, and possible microSD I/O pressure.
- The project already has input, dynamic resolution, streaming, and storage
  concepts that could map well.

Hard blockers:

- No Linux player build/run proof.
- No Steam Deck UI scale/readability proof.
- No controller glyph/input-layout proof.
- No microSD/persistent-data/storage pressure proof.
- No native plugin parity proof. `Assets/Plugins/x86_64/HectonAudioKernel.dll`
  is Windows-only by current file scan.
- No Proton path proof if Windows build is used instead of native Linux.

Verdict: good target after Windows flat route. Not ready now.

### macOS / Apple Silicon / Intel Macs

Static foundation: partial.

Current positives:

- macOS target settings exist.
- Unity supports Metal for macOS players, and URP is Metal-compatible.
- Source/docs contain Metal/Mac references.

Hard blockers:

- No macOS build/import proof.
- No Metal shader compile proof.
- No Apple Silicon vs Intel architecture proof.
- No native `.dylib` equivalent for Windows-only native plugin surfaces.
- No signing/notarization/app bundle path proof.
- Third-party packages may have editor/runtime/importer assumptions.

Unity Metal docs state Metal is supported for macOS players and URP, but Metal
does not support geometry shaders. HECTON-8 has custom shaders/compute; they
must be imported/compiled on Metal, not assumed portable from D3D/Vulkan.

Verdict: viable later, currently unproven and likely plugin/shader blocked.

### Consoles

Static foundation: not meaningful yet.

Unity serializes many console fields in `ProjectSettings.asset`, but first-party
script regex found no meaningful console-specific platform surface. Without SDK,
devkit, TRC/TCR/XR checks, save/storage/resume handling, native plugin parity,
certification constraints, and licensing review, console readiness is zero for
production purposes.

Verdict: strategic future target only. Do not spend implementation time on it
until the first playable route and PC/Linux/Mac/XR proof ladder exist.

## Content And Payload Readiness

Current filesystem facts:

- `Assets/AddressableAssetsData` exists but is empty.
- `Assets/_SourceData` exists but is empty.
- `Assets/StreamingAssets` currently contains only
  `signal_tuning_profiles.csv` and its `.meta`.
- The expected `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`
  is absent.

Important update: `H8StaticDataArena` is better than older docs imply. It has
platform-aware StreamingAssets URI staging to `Application.temporaryCachePath`,
FileStream reads, MemoryMappedFile path for direct filesystem cases, DataVault
BufferID storage, and telemetry dump paths.

That is progress, but not payload readiness. A platform loader without the
actual shipping blob is still a loader, not content proof.

## Third-Party And Native Plugin Risk

Current `Assets/Plugins` binary scan found many managed plugin DLLs and one
first-party/native-looking Windows binary:

- `Assets/Plugins/x86_64/HectonAudioKernel.dll`
- DOTween/Demigiant DLLs.
- Odin/Sirenix DLLs.
- Roslyn DLLs.
- RelationsInspector DLL.

This is not automatically fatal, but it blocks platform claims. Every runtime
binary needs a Windows/Linux/macOS/Android/console matrix or a code fallback.

For Quest/PICO/Steam Deck/macOS/consoles, "it compiles on Windows" is irrelevant
for native plugin loading.

## Progress Since Prior Global Audit

Real progress:

- Global authority docs now define boundaries, operating model, setup playbook,
  route cards, and review checklist.
- `AGENTS.md` now blocks casual global surface expansion.
- Copper Wire V0 route is documented as the first 20-minute product gate.
- SignalBus typed lanes are real and extensive.
- GlobalDataVault has real generation, defrag, owner release, and telemetry
  mechanics.
- FoveatedRenderCommander is real VR-facing code, not only a document.
- PlatformCompatibilityAudit and XrPlatformReadinessValidator exist and already
  identify many of the same blockers.
- XR package bootstrap has started: manifest now lists XR Management, OpenXR,
  and Unity Meta OpenXR; Android app id, target SDK, custom manifest, and custom
  Gradle settings have been corrected.
- XR validators now check custom Android manifest/Gradle usage and ARM64-only
  architecture in addition to package/template id/target SDK blockers.
- Scalability docs and runtime code are aligned around continuous
  `GlobalQualityWeight`.
- DataMonolith loader path improved toward Android/Quest hostile StreamingAssets
  handling.

Still absent:

- Fresh Unity import proof.
- Fresh player build proof.
- First 20-minute route proof.
- Target hardware profiler proof.
- Addressables project data/content groups.
- Static data monolith artifact.
- Unity package resolve/import proof for the new XR package ids.
- XR Plug-in Management provider settings and OpenXR/Meta feature group proof.
- Android VR device build/install/runtime proof.
- Linux/Steam Deck/native plugin proof.
- macOS Metal shader/native plugin proof.
- Console SDK/certification path.

## Correct Proof Ladder

Do not try to make every platform ready at once. Use this order:

1. Windows Editor import and console-clean proof for current tree.
2. Windows standalone flat build.
3. Copper Wire V0 route on Windows: boot, load, swim, copper collect, quest,
   craft, save, load, return.
4. Low-end PC capture: frame time, GC, memory, VRAM, hitch list, visual defects.
5. Content payload gate: `static_data.h8bin`, Addressables settings/groups,
   hash/freshness report.
6. Linux build and Steam Deck run: 1280x800 UI, controller glyphs, storage,
   frame pacing, native plugin behavior.
7. macOS import/build: Metal shader compile, native plugin parity, Apple
   Silicon/Intel decision.
8. Resolve/import XR packages, then configure XR Management + OpenXR + provider
   profiles.
9. PCVR smoke: headset start, input, comfort, UI, foveation disabled/enabled
   comparison.
10. Android non-XR IL2CPP ARM64 smoke.
11. Quest 3 standalone smoke, then Quest 2 stress pass.
12. PICO provider/package smoke after Quest path is stable.
13. Consoles only after the route is stable and platform vendor constraints are
   known.

## Senior Direction Call

Keep the architecture. Do not flatten it back into random MonoBehaviours.

But stop treating new global infrastructure as progress by itself. The right
near-term metric is:

```text
Does this reduce risk or increase proof for the Copper Wire route on at least one
real target?
```

If yes, continue. If no, park it unless it fixes a blocker in global authority,
payloads, platform configuration, or build/import.

## 2026-05-19 R10 Current Deep Scan

Evidence class: STATIC_SOURCE / PROJECT_SETTINGS / PACKAGE_SETTINGS /
FILESYSTEM. No Unity import, Package Manager resolve, build, Play Mode,
profiler, Quest/PICO run, Steam Deck run, macOS Metal run, or console SDK
validation was executed.

### Current Global Scanner Verdict

The project is not globally failing right now. It is still salvageable and is
moving in the correct architectural direction, but the global authority layer is
large enough that uncontrolled growth will turn it into a bottleneck.

Classification:

| Area | Classification | Reason |
|---|---|---|
| `GlobalRegistry` | Correct direction / architectural risk | Cold typed authority exists; generic `Get<T>` hot service-location pattern is not showing up, but 688 first-party files and 6169 dot hits prove high global surface pressure. |
| `SignalBus<T>` | Correct direction / missing proof | Typed unmanaged lanes are broad and real. 230 unique lane types exist; 216 are visibly configured or ensured; 9 producer-touching types lack obvious config proof in static scan. |
| `GlobalSignals` | Migration bridge / architectural risk | The bridge routes legacy traffic into typed lanes, but 259 `GlobalSignals.Publish` call sites mean the bridge can keep growing if agents use it for convenience. |
| `HectonEventBus` | Correct only for mod/API/cold use | The bus declares itself as a managed mod bridge, but 48 publish/subscribe sites need classification. Any first-party hot use is wrong. |
| `GlobalDataVault` | Strong core / ownership risk | Vault handles, owner IDs, defrag phase gates, generation checks, and dumps exist. Project-wide persistent native allocation is not fully under Vault/H8Memory discipline. |
| System phases | Correct direction / runtime proof absent | Dispatcher has PreSimulation, Simulation, PostSimulation, and VisualSync paths. Static phase order does not prove hitch-free job completion. |

Current static counters under `Assets/_Project/Scripts`:

| Signal | Count |
|---|---:|
| Files containing `GlobalRegistry.` | 688 |
| `GlobalRegistry.` dot hits | 6169 |
| `GlobalRegistry.Get<T>` / `TryGet<T>` hits | 0 |
| `SignalBus<...>` refs | 1309 |
| Unique `SignalBus<T>` payload types | 230 |
| Payload types visibly `Configure`/`EnsureInitialized` | 216 |
| Producer-touching payload types | 184 |
| Producer-touching payload types without obvious static config proof | 9 |
| `HectonEventBus.Publish/Subscribe` hits | 48 |
| `GlobalSignals.Publish` hits | 259 |
| `GlobalDataVault` / `IDataVault` / `VaultBufferHandle` / `DataVault` refs | 4286 |
| Native collection refs | 20667 |
| Persistent native constructor text hits | 959 |
| Exact `StructLayout(... Pack = 1)` hits | 135 |
| XR package lock hits in `Packages/packages-lock.json` | 0 |

Producer-touching `SignalBus<T>` payloads without obvious static config proof:

- `EncyclopediaUnlockSignal`
- `EntityDepletedSignal`
- `Hecton8.Core.Contracts.Signals.CameraFrustumSignal`
- `Hecton8.Core.Contracts.Signals.CameraPositionSignal`
- `Hecton8.Core.Contracts.Signals.CombatDamageSignal`
- `PlayVoiceOverSignal`
- `ResidencySectorDehydratedSignal`
- `ResidencySectorHydratedSignal`
- `ThermalUpdraftSignal`

This is not yet a hard blocker because aliases, fully qualified names, and
bridge configuration can confuse static regex. It is a required proof task
before claiming signal-lane GREEN status.

### Hard Blockers And Missing Proof

Hard platform blockers:

- `ProjectSettings/ProjectSettings.asset` still has
  `m_BuildTargetVRSettings: []`, so XR loader/provider settings are not
  generated.
- `Packages/manifest.json` contains XR Management, OpenXR, and Meta OpenXR, but
  `Packages/packages-lock.json` has zero matching lock entries. Unity has not
  resolved/imported the packages in this evidence pass.
- No Quest 2, Quest 3, PICO, PCVR, Steam Deck, macOS, or console runtime proof
  exists in this pass.
- 135 exact `Pack = 1` struct layouts are present in first-party runtime code.
  Every layout that enters Burst, NativeArray, SignalBus, GPU upload, file IO,
  save data, or network/native plugin boundaries must be audited for ARM64 and
  Metal/Vulkan alignment.
- 959 persistent native constructor text hits exist outside a single obvious
  Vault-only path. Many are probably cold, documented, or legitimate owner-local
  allocations, but DataVault sovereignty is not globally proven.
- BufferID duplicate sovereignty is now green for the central enum:
  `--fail-on-duplicates` passes. Full BufferID sovereignty is not proven because
  604 local numeric casts outside `H8Memory.cs` still need owner/range/lifetime
  burn-down or ledger proof.

Hard global failure status:

- Not currently proven. The global design has real authority primitives and
  guardrails. The failure mode is future drift, not a confirmed present collapse.

Missing proof:

- Unity import after package edits.
- Clean compile with current dirty tree.
- Copper Wire V0 route proof.
- Runtime profiler markers for dispatcher, signal flush, DataVault defrag, and
  XR/dynamic-resolution paths.
- GC allocation proof under the first 20 minutes.
- Device captures for low PC, Quest, Steam Deck, and macOS.

### Platform Direction Update

Windows flat PC remains the correct first proof target. It has the least
platform packaging friction and should prove the Copper Wire route before XR,
Deck, macOS, or consoles consume more architecture budget.

Low-end PC/MX350 direction is conceptually correct because the project has
quality pressure, dynamic resolution, streaming, and explicit low-tier budgets.
It is not proven because asset, shader, vendor package, and native allocation
pressure still need real frame-time, GC, VRAM, and hitch capture.

High-end PC direction is correct only as additive visual overkill after the low
baseline route is stable. Do not bake high-end visuals into mandatory systems.

PCVR has real runtime scaffolding: `HectonXRRuntimeState`, OpenXR package ids,
foveated rendering commander, and dynamic-resolution integration. It is blocked
by unresolved packages, empty XR provider settings, no loader assets, and no
headset smoke test.

Quest 3 is the first plausible standalone headset after Windows and PCVR proof.
Quest 2 is stricter and should be treated as a stress target, not the default
authoring baseline. Current Quest assets are useful, but `URP_Quest_VR.asset`
still has render scale 1 and MSAA 4; this may be too expensive until device
profiling proves it.

PICO should not be counted as "covered by Quest." It needs the PICO provider SDK
path, input/haptics/runtime proof, and separate device install.

Steam Deck is plausible after Windows because the project has Steam Deck-like
profiles and storage/scalability thinking. It still needs Linux or Proton proof,
controller/UI scale proof, storage pressure proof, and native plugin parity.

macOS is viable later, but Metal shader compile, Apple Silicon/Intel split,
native plugin parity, signing/notarization, and package importer behavior are
unproven.

Consoles remain zero production readiness. The architecture is not hostile to
consoles, but there is no SDK/devkit/TRC/TCR/certification path.

### Senior Call After R10

Keep `GlobalRegistry`, `SignalBus<T>`, `GlobalDataVault`, and the dispatcher.
They are the right spine for a multi-platform HECTON-8 if they stay governed.

Do not add more global surfaces as convenience plumbing. Every new global slot,
lane, vault buffer, or event route needs:

1. Owner.
2. Route card.
3. Capacity or lifecycle.
4. Phase.
5. Low/Mid/High/Ultra behavior through continuous `GlobalQualityWeight`.
6. Proof target tied to the Copper Wire route or a hard platform blocker.

Near-term work should be boring and strict:

1. Resolve Unity packages and regenerate `packages-lock.json`.
2. Configure XR Plug-in Management/OpenXR in Unity, not by hand-editing random
   YAML.
3. Prove Windows Copper Wire V0.
4. Classify the 48 `HectonEventBus` sites.
5. Prove or configure the 9 suspect `SignalBus<T>` producer payloads.
6. Audit `Pack = 1` structs by boundary.
7. Move persistent native allocations into DataVault/H8Memory or document
   owner-local waivers.
8. Only then run Quest/PCVR/Deck/macOS platform proof.

## External Platform Docs Consulted

- Unity XR project setup:
  https://docs.unity3d.com/Manual/configuring-project-for-xr.html
- Unity Meta Quest development:
  https://docs.unity3d.com/Manual/xr-meta-quest-develop.html
- Unity OpenXR Meta package:
  https://docs.unity3d.com/Manual/com.unity.xr.meta-openxr.html
- Unity URP untethered XR optimization:
  https://docs.unity.cn/6000.0/Documentation/Manual/urp/xr-untethered-device-optimization.html
- Unity Metal requirements:
  https://docs.unity.cn/6000.2/Documentation/Manual/metal-requirements-and-compatibility.html
- Valve Steam Deck tech specs:
  https://www.steamdeck.com/en/tech
- PICO Unity XR SDK quick start:
  https://sdk.picovr.com/docs/UnityXRSDK/en/chapter_four.html

## Final Status

Global architecture direction: correct, high-risk, not failed.

Platform readiness: Windows flat is the only sensible first proof target.
Everything else is scaffolded, not ready.

Progress: real. The project is better organized than before and has serious
portability intent in code. But without the Copper Wire route plus platform
build/profiler artifacts, the honest status remains `PENDING VERIFICATION`.

## 2026-05-19 R9 Current Recapture And Stable Policy Promotion

Evidence class: STATIC_SOURCE / STATIC_DOC / PROJECT_SETTINGS / PACKAGE_SETTINGS
/ FILESYSTEM / PY_TOOL. No Unity import, Package Manager resolve, build,
profiler, GCMonitor, player run, or device proof was executed.

Current dirty-tree pressure:

- `git status --short` shows `744` modified, `153` untracked, and `8` deleted
  entries. This audit is therefore direction and risk assessment, not readiness.
- `Packages/manifest.json` lists XR Management, OpenXR, and Unity Meta OpenXR.
  `Packages/packages-lock.json` still has no matching XR package lock entries,
  so Unity has not resolved/imported them in this workspace state.
- `ProjectSettings/ProjectSettings.asset` still has
  `m_BuildTargetVRSettings: []`; `Assets/XR` is absent. XR provider settings
  remain the next hard blocker.
- `Tools/DataVaultSovereigntyAudit.py --fail-on-regression` currently fails:
  `direct=1064`, `allowed=6`, `forbidden=1058`, `files=161`,
  `declarations=4645`, `forbiddenDeclarations=4639`,
  `declarationFiles=323`, with the no-regression baseline missing. This is
  expected for an incomplete migration but blocks any claim that DataVault
  sovereignty is already enforced.

Current static orientation under `Assets/_Project/Scripts`:

| Signal | Lines | Files |
|---|---:|---:|
| `GlobalRegistry.` | 6075 | 687 |
| `GlobalSignals.Publish` | 259 | 91 |
| `HectonEventBus.Publish` | 19 | 12 |
| `SignalBus<...>.Push/TryPush` | 285 | 101 |
| `SignalBus<...>.EnsureInitialized` | 260 | 54 |
| `SignalBus<...>.Configure` | 219 | 42 |
| `new NativeArray<...>` | 1064 | 162 |
| `NativeArray<...>` | 13383 | 555 |
| `GlobalQualityWeight` | 1370 | 207 |
| Steam Deck references | 131 | 21 |
| Quest/OpenXR references | 127 | 23 |
| PICO references | 3 | 2 |
| Mac/Metal references | 310 | 71 |
| Vulkan references | 48 | 12 |

Documentation gap scan result:

- Stable global-authority policy is sufficient: `GLOBAL_AUTHORITY_BOUNDARIES`,
  `GLOBAL_AUTHORITY_OPERATING_MODEL`, `GLOBAL_AUTHORITY_SETUP_PLAYBOOK`,
  route card, review checklist, migration ledger, `AGENTS.md`, and
  `QUALITY_GATES.md` all carry the correct senior direction.
- Platform ladder policy was too dependent on this dated report. The durable
  rule is now promoted to
  `Docs/ARCHITECTURE/PLATFORM_PORTABILITY_PROOF_LADDER.md` and linked from
  `AGENTS.md`, `.codexrules/AGENTS.md`, `Docs/README.md`,
  `Docs/ARCHITECTURE/README.md`, and `Docs/QUALITY_GATES.md`.

R9 verdict remains unchanged: direction is correct, but platform and global
authority readiness claims are blocked until the proof ladder produces artifacts.

## 2026-05-19 R13 Current Global Direction And Portability Recap

Evidence class: STATIC_SOURCE / STATIC_DOC / PROJECT_SETTINGS /
PACKAGE_SETTINGS / PY_TOOL. No Unity import, Package Manager resolve, dotnet
build, Unity Console, Play Mode, profiler, GCMonitor, player build, headset
smoke, Steam Deck run, macOS run, PICO run, or console SDK validation was
executed.

Dirty workspace pressure:

| Signal | Current Value |
|---|---:|
| `git status --short` total entries | 999 |
| Modified entries | 792 |
| Untracked entries | 199 |
| Deleted entries | 8 |

Current first-party static source counters:

| Surface | Matches | Files |
|---|---:|---:|
| `GlobalRegistry.` | 6174 | 689 |
| `GlobalRegistry.Get<T>` | 0 | 0 |
| `SignalBus<...>` refs | 1321 | 198 |
| `SignalBus<...>.Push/TryPush` | 288 | 104 |
| `SignalBus<...>.Configure` | 223 | 45 |
| `SignalBus<...>.EnsureInitialized` | 265 | 57 |
| `GlobalSignals.Publish` | 259 | 91 |
| `HectonEventBus.Publish/Subscribe` | 48 | 20 |
| `GlobalDataVault` / `IDataVault` / `VaultBufferHandle` / `DataVault` refs | 4808 | 272 |
| `new NativeArray<...>` | 1064 | 162 |
| `Pack = 1` | 154 | 50 |
| `GlobalQualityWeight` | 1680 | 217 |
| Quest/OpenXR refs | 2684 | 100 |
| PICO refs | 3 | 2 |
| Steam Deck/Linux/Vulkan refs | 268 | 36 |
| macOS/Mac/Metal refs | 2610 | 161 |

Current gate results:

- `python Tools/DataVaultSovereigntyAudit.py --fail-on-regression`: FAIL,
  `direct=1064`, `allowed=6`, `forbidden=1058`, `files=161`,
  `declarations=4659`, `forbiddenDeclarations=4653`,
  `declarationFiles=324`; no-regression baseline missing.
- `python Tools/BufferIDSovereigntyAudit.py --fail-on-duplicates`: PASS after
  R15 verification, `duplicates=0`, `localCasts=604`, `castFiles=50`.
- SignalBus producer/config comparison: 183 producer payload types, 216
  configured/initialized payload types, 9 producer-touching payload types still
  lacking obvious static config proof:
  `EncyclopediaUnlockSignal`, `EntityDepletedSignal`,
  `Hecton8.Core.Contracts.Signals.CameraFrustumSignal`,
  `Hecton8.Core.Contracts.Signals.CameraPositionSignal`,
  `Hecton8.Core.Contracts.Signals.CombatDamageSignal`,
  `PlayVoiceOverSignal`, `ResidencySectorDehydratedSignal`,
  `ResidencySectorHydratedSignal`, `ThermalUpdraftSignal`.

### Senior Verdict

The global direction is still correct. The project is not in terminal
architecture failure.

The shape is right:

- `GlobalRegistry` is broad, but generic hot `GlobalRegistry.Get<T>` is still
  absent in the static scan. That supports the intended cold-registry model.
- `SignalBus<T>` is the correct first-party broadcast model and has real
  configuration surface.
- `GlobalSignals` is acceptable only as bridge infrastructure. Its 259 publish
  hits are too many to treat as harmless, but not proof of collapse.
- `HectonEventBus` is mostly in mod/meta/progression/API-looking areas. It still
  needs classification, because 48 managed publish/subscribe hits are enough for
  future misuse.
- `GlobalDataVault` is the right tool for cross-domain/job/scene/save/crash
  native state. R12 fixed the central duplicate `BufferID` alias. The remaining
  issue is local numeric cast debt and raw native collection ownership, not the
  existence of the vault.

The dangerous vector is horizontal system growth before route proof. A high
H-Phi or high GlobalQualityWeight count does not make the project playable.
Until Windows/Copper Wire V0 is proven, platform work must remain scaffold and
gate work, not readiness marketing.

### Platform Readiness Bands

These are static readiness bands, not production readiness claims.

| Platform | Current Band | Honest Meaning |
|---|---:|---|
| Windows flat PC, mid/high | 50-60% | Correct first proof target; architecture scaffolding exists, current dirty workspace lacks fresh Unity import/Play/profiler proof. |
| Windows low PC / MX350 | 40-50% | Budget doctrine exists; actual asset, shader, VRAM, and frame-time proof absent. |
| Steam Deck / Linux / Proton | 30-40% | Linux/Vulkan references and scaling intent exist; no Deck/Proton player proof, controller/UI/storage/native-plugin parity unproven. |
| macOS / Metal | 25-35% | Metal/macOS references exist; shader compile, Apple Silicon/Intel split, signing/notarization, native plugin parity unproven. |
| PCVR | 30-40% | XR runtime code and OpenXR package IDs exist; package lock, XR loader/provider assets, headset smoke, comfort proof missing. |
| Quest 3 standalone | 25-35% | Android package id, target SDK, ARM64-only, manifest/Gradle, XR package IDs exist; Unity resolve, XR provider settings, device build/install/thermal/comfort proof missing. |
| Quest 2 standalone | 20-30% | Same as Quest 3 but harsher GPU/thermal budget; treat as stress target. |
| PICO standalone | 10-15% | PICO references are almost absent; Quest setup does not count as PICO provider/input/haptics proof. |
| Consoles | 5-10% | Architecture not hostile to consoles, but no SDK/devkit/TRC/TCR/certification path exists. |

### Correct Next Order

1. Keep global authority tools, but freeze convenience growth.
2. Resolve Unity packages and regenerate `packages-lock.json`.
3. Configure XR Plug-in Management/OpenXR provider settings through Unity.
4. Prove Windows Copper Wire V0: boot -> world -> copper collect -> copper wire
   craft -> save/load.
5. Classify all 48 `HectonEventBus` publish/subscribe sites.
6. Add/verify config proof for the 9 suspect SignalBus producer payloads.
7. Burn down or ledger the 604 local numeric BufferID casts.
8. Run Pack=1 ARM64 boundary audit and convert runtime DTOs to aligned layouts
   unless they are cold file-format records.
9. Only after Windows route proof, climb: low-end PC capture -> Steam Deck ->
   macOS -> PCVR -> Quest 3 -> Quest 2 -> PICO -> consoles.

### Autonomous Improvement Performed

R12 removed the hard central `BufferID` alias:

- `SaveWorldPagerWriteArena` remains `70200`.
- `ConstructionBuilderOccupancy` is now `70319`.
- `Tools/BufferIDSovereigntyAudit.py --fail-on-duplicates` now passes.

This does not make the game ready. It removes one real native-state corruption
class from the global authority spine.

## 2026-05-19 R14 Unified Global Authority Gate

Evidence class: STATIC_SOURCE / PY_TOOL. No Unity import, dotnet build, Play
Mode, profiler, player build, or device proof was executed.

Autonomous improvement: added `Tools/GlobalAuthorityGate.py` and
`Tools/test_global_authority_gate.py`.

Purpose: one read-only command that exposes the current global-authority pressure
without writing reports by default. It fails only on hard checks by default:
generic `GlobalRegistry.Get<T>` / `TryGet<T>` usage and duplicate central
`BufferID` values. Other pressure is reported as warnings until the migration
burn-down makes those flags practical hard gates.

Current run:

```text
python Tools/GlobalAuthorityGate.py
status=PASS_WITH_WARNINGS
globalRegistryDot=6185 files=690
globalRegistryGenericGet=0 files=0
signalBusRefs=1318 files=198
signalBusPushTryPush=288 files=104
signalBusConfigure=220 files=43
signalBusEnsureInitialized=264 files=56
globalSignalsPublish=259 files=91
hectonEventBusPubSub=46 files=20
dataVaultRefs=4846 files=275
nativeArrayCtor=1057 files=161
nativeCollectionRefs=15491 files=622
packOne=154 files=50
localNumericBufferCast=604 files=50
signalBusSuspects=9
bufferId=duplicates=0 localCasts=604 castFiles=50
```

Interpretation: hard authority gate is clean after R12. The project still has
warnings that block green readiness claims: broad registry surface, 259
`GlobalSignals.Publish` hits, 46 `HectonEventBus` publish/subscribe hits, 9
SignalBus producer/config gaps, 604 local numeric `BufferID` casts, 1057 direct
`new NativeArray<...>` hits, and 154 `Pack = 1` hits.

This reinforces the same verdict: direction correct, proof missing, governance
still required.

## 2026-05-19 R15 Platform Proof-Adjusted Correction

Evidence class: STATIC_DOC / STATIC_SOURCE / PROJECT_SETTINGS /
PACKAGE_SETTINGS / FILESYSTEM. No Unity import, dotnet build, Play Mode,
profiler, GCMonitor, player build, headset smoke, Steam Deck run, macOS run,
PICO run, or console SDK validation was executed.

R13 used static scaffolding bands. R15 records the stricter proof-adjusted view:
how much is actually proven in the current dirty workspace. This is the safer
number for planning.

| Target | Static Scaffold | Proven Runtime | Verdict |
|---|---:|---:|---|
| Windows low / MX350 | 25-35% | 0-5% | Correct first target, but no MX350 capture exists. |
| Windows mid PC | 35-45% | 0-5% | Best first Copper Wire platform; still no fresh player proof. |
| Windows high PC | 40-50% | 0-5% | Visual-overkill intent exists; high tier cannot be proven before low baseline. |
| Steam Deck / Linux native | 15-25% | 0% | PAL/scaling intent exists; no Linux run, `.so` parity, shader/runtime proof. |
| Proton path | 10-20% | 0% | Windows build does not prove Proton. |
| macOS / Metal | 10-20% | 0-2% | Metal intent exists; no dylib/sign/notarization/Metal compile proof. |
| PCVR | 15-25% | 0% | XR runtime code exists; provider/settings/headset proof absent. |
| Quest 3 standalone | 15-25% | 0% | Android/Vulkan/ARM64 scaffold exists; XR loader/import/device proof absent. |
| Quest 2 standalone | 10-20% | 0% | Same blocker as Quest 3, with harsher thermal/fill-rate constraints. |
| PICO standalone | 3-8% | 0% | PICO is effectively unconfigured. Quest/OpenXR scaffold is not PICO proof. |
| Consoles | 0-3% | 0% | No SDK/devkit/TRC/TCR/certification path. |

Confirmed blockers:

- `Packages/manifest.json` lists XR Management, OpenXR, and Meta OpenXR, but
  `Packages/packages-lock.json` has no matching XR package lock entries.
- `ProjectSettings/ProjectSettings.asset` still has
  `m_BuildTargetVRSettings: []`.
- `ProjectSettings/XRSettings.asset` contains only legacy keys:
  `VR Device Disabled` and `VR Device User Alert`.
- Android scaffold is partially correct: app id, target SDK 35, ARM64-only,
  custom manifest, and custom Gradle template are present. Build/install/launch
  proof is absent.
- Native plugin parity is Windows-only in the checked inventory:
  `Assets/Plugins/x86_64/HectonAudioKernel.dll` and
  `Assets/_Project/Plugins/Windows/x86_64/liblz4.dll`; no Linux/macOS/Android
  equivalents were found for these key paths.
- `Assets/AddressableAssetsData` exists but is empty.
- `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is absent.

Adjusted platform conclusion: for architecture direction, the vector is right.
For production/platform readiness, the honest answer is still near-zero proof
outside static scaffolding. Windows Copper Wire remains the only rational first
proof path.

## 2026-05-19 R16 Current Gate Recapture

Evidence class: PY_TOOL / STATIC_SOURCE / STATIC_DOC. No Unity import, dotnet
build, Play Mode, profiler, player build, or device proof was executed.

Current hard gates:

- `python Tools/GlobalAuthorityGate.py`: PASS_WITH_WARNINGS.
- `python Tools/BufferIDSovereigntyAudit.py --fail-on-duplicates`: PASS.
- `python Tools/DataVaultSovereigntyAudit.py --fail-on-regression`: FAIL,
  baseline missing; current counts `direct=1057`, `allowed=6`,
  `forbidden=1051`, `files=160`, `declarations=4700`,
  `forbiddenDeclarations=4694`, `declarationFiles=327`.

Current global authority gate output:

```text
globalRegistryDot=6185 files=691
globalRegistryGenericGet=0 files=0
signalBusRefs=1323 files=199
signalBusPushTryPush=288 files=104
signalBusConfigure=221 files=44
signalBusEnsureInitialized=264 files=56
globalSignalsPublish=259 files=91
hectonEventBusPubSub=46 files=20
dataVaultRefs=4896 files=280
nativeArrayCtor=1057 files=161
nativeCollectionRefs=15599 files=626
packOne=156 files=51
localNumericBufferCast=609 files=50
signalBusSuspects=9
bufferId=duplicates=0 localCasts=609 castFiles=50
```

R16 interpretation:

- Correct direction remains unchanged. The hard global-authority tripwires are
  clean: no generic registry get/try-get and no central BufferID duplicate.
- Warning pressure is not shrinking. Registry breadth, DataVault refs, native
  collection refs, local BufferID casts, and `Pack = 1` all remain too large for
  green status.
- DataVault itself is still the right architecture. The failing gate is the
  migration/no-regression baseline and surrounding native ownership debt.
- Platform proof status remains R15: scaffold exists, proven runtime readiness
  is near zero until current Unity/player/device artifacts exist.

## 2026-05-19 R17 Final Recapture For Repeated Request

Evidence class: PY_TOOL / STATIC_SOURCE. No Unity import, dotnet build, Play
Mode, profiler, player build, or device proof was executed.

Latest repeated gate run:

- `python Tools/GlobalAuthorityGate.py`: PASS_WITH_WARNINGS.
- `python Tools/BufferIDSovereigntyAudit.py --fail-on-duplicates`: PASS.
- `python Tools/DataVaultSovereigntyAudit.py --fail-on-regression`: FAIL,
  baseline missing; current counts `direct=1057`, `allowed=6`,
  `forbidden=1051`, `files=160`, `declarations=4704`,
  `forbiddenDeclarations=4698`, `declarationFiles=327`.

Latest authority counters:

| Surface | Matches | Files |
|---|---:|---:|
| `GlobalRegistry.` | 6185 | 691 |
| `GlobalRegistry.Get/TryGet` | 0 | 0 |
| `SignalBus<...>` refs | 1323 | 199 |
| `SignalBus<...>.Push/TryPush` | 288 | 104 |
| `SignalBus<...>.Configure` | 221 | 44 |
| `SignalBus<...>.EnsureInitialized` | 264 | 56 |
| `GlobalSignals.Publish` | 259 | 91 |
| `HectonEventBus.Publish/Subscribe` | 46 | 20 |
| DataVault refs | 4897 | 280 |
| `new NativeArray<...>` | 1057 | 161 |
| Native collection refs | 15603 | 626 |
| `Pack = 1` | 156 | 51 |
| local numeric `(BufferID)N` casts | 609 | 50 |
| SignalBus producer/config suspects | 9 | - |
| central BufferID duplicate values | 0 | - |

R17 verdict: no change. Direction is correct, hard global-authority tripwires
are clean, warning pressure remains high, and platform readiness remains
artifact-blocked.
