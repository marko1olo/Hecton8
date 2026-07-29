# Contributing to HECTON-8

This document exists because several facts about this repository are non-obvious, cost real time to
rediscover, and are not visible from the code. Read it before non-trivial work.

- [Authority chain](#authority-chain)
- [Verifying a change](#verifying-a-change)
- [Traps in this repository](#traps-in-this-repository)
- [Working alongside other contributors](#working-alongside-other-contributors)
- [Commit conventions](#commit-conventions)
- [Finding real defects](#finding-real-defects)

---

## Authority chain

For anything beyond a typo, read in this order. Read documents whole before acting on them; text
search is for navigation and audit, not for comprehension.

1. `AGENTS.md` — canonical agent law
2. `COMMON_SENSE.md` — architectural constraints for non-trivial work
3. `Docs/AGENT_AUTHORITY_ROUTING.md` — routing protocol
4. `PROJECT_BIBLES.md` — selects which route bible applies
5. `VISION_LOCKS.md` — resolves ambiguity, priority and taste conflicts
6. `TASTE.md` — required for player-visible work
7. The matching **route bible** at repo root (`world.md`, `ecosystem.md`, `rendering.md`, …)

This list is a human-facing summary, not the authority chain itself. The chain is defined once, in
`AGENTS.md` `Task Intake`, and additionally requires `Docs/HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`,
`Docs/SYSTEMS_CONTRACTS.md` for runtime/architecture work, `.agents-skills/README.md` plus every mandate
the task domain touches, and `Docs/QUALITY_GATES.md` before any `VERIFIED`/`COMPLETE` claim. Follow the
`AGENTS.md` version if the two ever disagree, and do not grow agent law in this file.

There is no cap on how much of that you may read — the old context-budget limits were retired 2026-07-27.
Full bibles, full mandate bodies and full logs are expected when the task touches them, and `2-8` mandates
is a floor rather than a quota.

Current source, current assets and fresh proof outrank dated reports, generated snapshots, task
logs and archives. If a report and the code disagree, the code is right and the report is stale.

---

## Verifying a change

### Lock-free compile gate — use this by default

Unity ships its own .NET SDK, so you can compile **without holding the Unity project lock**. This
matters because another contributor may have the editor open.

```bash
"C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Data\DotNetSdk\dotnet.exe" \
  build Hecton8.Core.csproj \
  -p:UnityEditorManagedDir="C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Data\Managed" \
  -v:minimal --nologo
```

Roughly 35 seconds for `Hecton8.Core`. Output goes to `Temp/CodexBuild/`.

> [!WARNING]
> **The build output is localised.** On a Russian-locale machine it prints `Ошибок: 0` /
> `Предупреждений: 5`. Grepping for `Error` finds nothing and looks like success. Grep the localised
> word, or grep for `error CS`.

> [!CAUTION]
> **This gate produces false errors on `Hecton8.Editor.csproj`.** It reports `CS0433` (duplicate
> `VerletCableSimulator` / `CableConstraintSatisfier`) and `CS0656`
> (`Microsoft.CSharp.RuntimeBinder`). Neither is real:
> - the "duplicate" type is declared exactly once, in `PureLogic/Systems/`. The standalone csproj
>   compiles PureLogic sources into `Hecton8.Core` *and* references `Hecton8.PureLogic`, so one file
>   appears in two assemblies. Unity's Bee build resolves asmdef ownership correctly.
> - `CS0656` is `dynamic` requiring a `Microsoft.CSharp` reference that Unity supplies and a bare
>   csproj does not.
>
> Unity's real compile reports neither. **For Editor-assembly code, trust only a Unity batchmode
> run.** An agent following the gate blindly here will "fix" three bugs that do not exist.

> [!CAUTION]
> **The lock-free gate compiles in EDITOR configuration and is blind to player-build breakage.**
> `Directory.Build.props:10` and `Directory.Build.targets:39,128` inject `UNITY_EDITOR;UNITY_EDITOR_WIN`
> on top of Unity's generated csproj, so `dotnet build` is green while the shipped player does not
> compile — and stays green indefinitely. **68 real defects accumulated behind this.**
>
> The failure shape: an `#if UNITY_EDITOR` region opened for a legitimate CSV or authoring route, then
> not closed, swallowing runtime members that unguarded code calls. Every one is `CS0103`, and nothing
> in an editor-configuration build can see any of them.
>
> Before claiming an assembly is clean, run the player configuration:
>
> ```bash
> python -B Tools/PlayerConfigCompileGate.py --assembly Hecton8.Core --also-editor
> ```
>
> `--also-editor` compiles both, which is what you want — the fix must not trade a player error for an
> editor one. `--all-runtime` sweeps every first-party assembly that ships. Exit code 1 on failure.
>
> **Do not run the lock-free editor gate first and then `--all-runtime` in the same tree.** Both write to
> `Temp/CodexBuild/`, so an editor-configuration build leaves artifacts that the player sweep then links
> against, and the mismatch surfaces as a `CS0103` in a file that is perfectly fine. Measured 2026-07-29:
> a plain `dotnet build Hecton8.Core.csproj` followed by `--all-runtime` reported `1 player error` in
> `HectonPlayerSpawner.cs`; `--assembly Hecton8.Core` alone reported 0, and a clean `--all-runtime`
> reported **0 across all seven shipping assemblies**. Chasing that phantom cost three runs and a hunt for
> an unbalanced `#if` that did not exist — the directives in that file are balanced, final depth 0. If the
> sweep disagrees with a single-assembly run, distrust the sweep that came second, not the source.
>
> **`--all-runtime` sweeps the csprojs that exist on disk, and most assemblies do not have one.** The 56
> `.csproj` files at the repo root are hand-written, not Unity-generated, and they are **gitignored**
> (`.gitignore:68  *.csproj`) — so they exist only where somebody typed them, a fresh clone has none, and
> nothing regenerates them. The scaffold is frozen `2026-06-12`. Measured 2026-07-29: **179 first-party
> `Hecton8.*` assemblies in `Library/ScriptAssemblies`, 7 with a csproj.** Any asmdef newer than the
> scaffold is invisible to both gates, permanently and silently. `Hecton8.Plugins` — which owns the
> MapMagic terrain bridge, where a non-compiling change breaks world generation — was two days newer than
> the scaffold and had never been compiled by any automated check in this repo.
>
> Generate a missing gate project from the asmdef:
>
> ```bash
> python -B Tools/GenerateAssemblyCompileGateProject.py Hecton8.Plugins
> python -B Tools/GenerateAssemblyCompileGateProject.py Hecton8.Plugins --print-build-command
> ```
>
> The generator is the tracked artifact; the `.csproj` it emits is not, and that is deliberate given the
> ignore rule. **Its reference set is the whole difficulty, so do not hand-narrow it.** A gate that
> references MORE than Unity does invents errors: the first run against `Hecton8.Plugins` reported three
> `CS0234` on `Time.time` in `MapMagicRuntimeBridge.cs` purely because the wildcard pulled in
> `Hecton8.Core.Time.dll`, whose namespace `Hecton8.Core.Time` then shadows `UnityEngine.Time` for every
> file inside a `Hecton8.*` namespace — the same family as `Hecton8.Environment` vs `System.Environment`.
> Unity compiles that assembly cleanly *because* its asmdef does not reference it. A gate that references
> LESS invents `CS0246` instead: excluding the asmdef's own `Den.Tools` and `MapMagic` produced **470**
> of them. So the rule is vendor/package/Unity DLLs on the broad wildcard, first-party filtered to exactly
> what the asmdef declares, and both sets derived from the asmdef rather than typed.
>
> ### Before citing a gate as proof, ask whether it compiled the file you changed
>
> A gate reporting `0 errors` proves nothing about your edit unless that edit is inside the assembly it
> built. This is not a theoretical caution — commit `60a7ed08d` cited
> `PlayerConfigCompileGate --assembly Hecton8.Core --also-editor -> EDITOR 0, PLAYER 0` as proof for two
> files under `Assets\_Project\Scripts\QA\Headless\`, and `Hecton8.Core.csproj:118-119` **removes** that
> whole directory. The `0/0` was real and irrelevant. It had to be retracted in `3484e3a4d`.
>
> The csprojs use glob includes with a long tail of `<Compile Remove>` exclusions, so eyeballing them is
> unreliable — `Assets\_Project\Scripts\World\` is included while six of its own subdirectories are not.
> Ask MSBuild instead. It costs seconds, it does not build, and it answers the question exactly:
>
> ```bash
> "C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Data/DotNetSdk/dotnet.exe" \
>   msbuild Hecton8.Core.csproj -getItem:Compile -nologo | rg 'YourChangedFile\.cs'
> ```
>
> A hit means the gate's verdict covers your file and you may cite it. No hit means the gate is silent
> about your change no matter what it printed, and you must say `UNCOMPILED` and name the reason — as with
> the `Hecton8.QA.Headless*` assemblies, which have no csproj at all and are only reachable through Unity
> batchmode. Verified working 2026-07-29 on `EcosystemDirector.cs` against `Hecton8.Core.csproj`.
>
> Same discipline, different axis, already recorded in `.claude\rules\hecton8-runtime-source.md`: after a
> `.cs` edit, confirm from the batchmode log that Bee actually **recompiled** the target assembly rather
> than serving a cache hit. "Is my file in this assembly" and "did this assembly rebuild" are two separate
> questions and a citation needs both.
>
> Result on `Hecton8.Plugins`, 2026-07-29: `Ошибок: 0`, one pre-existing `CS0649` —
> `MapMagicRuntimeBridge.distantTerrainShadowMaskOverride` (`MapMagicRuntimeBridge.cs:149`) is never
> assigned, so it is always `null`; that is a real finding this gate surfaced on its first run. Dependencies
> bind as prebuilt DLLs out of `Library/ScriptAssemblies`, so a generated gate is only as fresh as Unity's
> last successful compile of those dependencies. That is a real limitation of this gate, not a hidden one.
>
> Fix by **moving the guard boundary**, not by guarding the call site — *unless the feature really is
> editor-only*. Determining which requires reading what the code actually touches, not what it is
> named. A worked example of the exception: `ShinobuFloraFaunaSymbiosisSolver
> .TryLoadLegacyLinksIntoVault` looked like shippable data loading, and "data files ship with the
> game" is true in general. But its path builder resolves the parent of `Application.dataPath` plus
> `Docs/Archive/` — the repository root. That cannot exist in a player, so it is a one-time authoring
> migration and the call site is the correct place to guard. Read the path builder before deciding.
>
> A useful tell for an over-wide guard: a **redundant nested `#if UNITY_EDITOR`** further down. Nesting
> is pointless unless its author believed they were outside a guard — and they were not.
>
> `Tools/EditorGuardLeakScanner.py` is the fast pre-filter and prints declared-vs-called line numbers,
> which is what makes the fix mechanical. It structurally cannot see guarded *fields* or partial
> classes split across files, so when the two disagree, the compile gate wins.

### Runtime proof

Compiling proves nothing about behaviour. The two probes in
[README → Verification](README.md#verification) are the cheapest real evidence; both exit non-zero
on failure.

If the Unity lock is held and you only need to know whether *someone else's* build already covered
your change, read their log instead of launching a second editor:

- `Logs/*.log` for the run, and `Library/Bee/artifacts/` for assembly timestamps.
- A `.dll` newer than your source edit, with `Ошибок: 0`, is proof your file compiled — Csc emits no
  assembly on error.

> [!TIP]
> To confirm a symbol actually reached a built assembly, **`strings` does not work** on these DLLs —
> it returns zero hits even for symbols that are definitely present. Use `grep -ac <symbol> <dll>`,
> and always run a control symbol you know exists, so a broken probe cannot masquerade as a negative
> result.
>
> **Member names are ASCII; string literals are UTF-16LE.** Metadata names live in the `#Strings` heap
> as UTF-8, so `grep -ac MyMethodName` finds them. String *literals* live in the `#US` user-string heap
> as UTF-16, so `grep -ac "my_folder_name"` returns **0 for a literal that is present** — a false
> negative that reads exactly like "my change did not compile". Measured 2026-07-29 on
> `Hecton8.Editor.dll`: six `Logs/` subfolder literals returned ascii=0 / utf-16le=1 each, while three
> new method and field names returned ascii=1. Probe literals with the UTF-16 encoding:
>
> ```python
> d = open('Library/ScriptAssemblies/Hecton8.Editor.dll','rb').read()
> print(d.count(b'my_folder_name'), d.count('my_folder_name'.encode('utf-16-le')))
> ```
>
> **This is how to get an honest gate on `Hecton8.Editor` without launching Unity.** The lock-free
> `dotnet build` emits phantom `CS0433`/`CS0656`/`CS0103` on that assembly and therefore cannot verify
> it, so Editor-assembly changes have historically shipped with no compile proof at all. But Csc emits
> no assembly on error, so if *any* session has an editor open, `Library/ScriptAssemblies/<Asm>.dll`
> with an mtime later than your edit, containing a symbol your edit introduced, is genuine
> compiler-grade proof that your file compiled. Check the mtime, probe a new symbol in the right
> encoding, and probe a control. That is cheaper than a batchmode run and does not fight anyone for the
> lock — but it only proves compilation, never behaviour.

### Never launch a second Unity editor on this project

Unity does not support concurrent editor instances on one project folder. Check first:

```bash
ls Temp/UnityLockfile           # exists => locked
powershell -c "(Get-Process Unity -ErrorAction SilentlyContinue|Measure-Object).Count"
```

If locked, use the lock-free gate or read the running build's log. Launching anyway risks corrupting
the `Library/` another contributor is using.

---

## Traps in this repository

### `Hecton8.Environment` shadows `System.Environment`

The project declares a `Hecton8.Environment` namespace. Any file under the `Hecton8.*` namespace
root that writes bare `Environment` binds to **that namespace**, not to `System.Environment`, and
fails with:

```
error CS0234: The type or namespace name 'GetCommandLineArgs' does not exist
              in the namespace 'Hecton8.Environment'
```

Always fully qualify:

```csharp
string[] args = System.Environment.GetCommandLineArgs();
```

This has broken the build at least once and been narrowly avoided at least once more. It is a
standing hazard, not a one-off.

### `int2 ==` returns `bool2`

Unity.Mathematics vector comparison is component-wise. `if (a == b)` on `int2` does not compile as
you expect. Use `a.Equals(b)`.

### Integer promotion in `const` expressions

`someIntConst - 1u` promotes to `long` and will not assign to a `uint` const (`CS0266`). Cast
explicitly: `(uint)someIntConst - 1u`.

### Seeding a min-fold with a sentinel

A real defect found in this codebase: a loop folding the *earliest* activation time initialised its
accumulator to a large **negative** sentinel, so `math.min(sentinel, candidate)` returned the
sentinel on every iteration. The whole feature was inert and nothing errored. Seed min-folds with
`float.MaxValue` and lift invalid candidates to `float.MaxValue` so they cannot win, rather than
masking after the fold.

### Silent degeneracy is the dominant failure mode here

Because so much is procedural, a broken system usually does not throw — it produces uniform,
plausible-looking output. Three real examples: vegetation whose bioluminescent pulse could never
fire; ecosystem biome derived from a coordinate hash entirely decorrelated from the visible seafloor;
and the fix for that second one, which replaced the hash with a **depth-blind** mask test and
labelled 700 m abyssal terrain a rich photic shelf. All three compiled, ran, and logged nothing.

**If a system can silently collapse, write a probe that fails loudly instead.**
`EcosystemGeologyBiomeLanes.SampleLaneDistribution` + `H8_GeologyBiomeLaneProbe` are the reference
pattern: the canonical mapping lives in one shared unit that both the runtime and the audit call, so
the audit cannot drift and report health while the runtime is degenerate.

> [!CAUTION]
> **A shared unit is necessary but not sufficient, and the third example above is why.** That version
> *had* the shared unit and *had* the probe. Both were wrong together, because both were built from
> the same author's premise: the runtime tested `ShelfMask > 0.5`, and the probe dutifully reported
> `maxShelfMask` against that same threshold. The audit could not contradict the code because it
> shared the code's assumption. It reported `DISCRIMINATING` and a plausible 50 % shelf share, and
> that number was published as proof.
>
> The shared-unit pattern prevents **drift**. It does nothing about a **shared wrong premise**.
>
> Two rules follow, and they are worth more than the pattern itself:
>
> 1. **Assert against the authority's output, not against your own re-derivation.** The mapping now
>    switches on `WorldMacroGeologySample.PrimaryZone`, and the probe reports the *zone counts*
>    (`photicShelf`, `shelfBreak`, `brineTrench`, `hadalBasin`) that produced each lane. A rich share
>    with zero contributing shelf zones is now a visible contradiction. Before, there was nothing the
>    output could contradict.
> 2. **Make the probe's output falsifiable, not merely informative.** "Both masks reach 1.0" cannot
>    be wrong. "545 rich sectors, of which 0 are PhoticShelf and 0 are ShelfBreak" can be — and that
>    is the whole value.
>
> If you find yourself writing a diagnostic that can only agree with the code it tests, you have
> written a mirror, not a check.

**Corollary — do not re-derive rules an authority already owns.** The depth-blind bug existed because
the lane mapping re-implemented zone classification instead of calling
`WorldMacroGeologyFields.ResolveZone`, which already encoded `ShelfMask > 0.68 && Depth < 260` and
resolved shelf break from a different field entirely (`ShelfBreakMask`). Swept the rest of the
codebase for siblings afterwards: the only other mask comparisons outside the geology owner are
`ProceduralWreckGenerator` asking "is this ridge-like enough to site a wreck" and
`WorldTerrainDetailContracts` blending surface materials. Both ask genuinely local questions rather
than re-deriving zones, so both are legitimate. If you add a third, check which of those two kinds it
is before you ship it.

---

## Working alongside other contributors

This tree is worked in parallel, frequently by automated agents. Assume the working tree contains
someone else's half-finished work at all times.

- **`git add` your own files by name.** Do not `git add -A` unless you intend to snapshot others'
  work, and say so in the message if you do.
- **Do not revert what you did not write.** If you suspect a regression in someone else's code,
  isolate the evidence and report the owning route.
- **An automated cement process snapshots the tree roughly every 30 minutes** and will claim your
  new files into a generic commit, losing your rationale. Commit promptly after writing a file.
- **Re-verify blocker claims before repeating them.** A build break attributed to another
  contributor was fixed ~30 minutes before it was reported as still live. Check, then speak.

---

## Commit conventions

```
type(scope): what changed and why it was wrong before
```

Bodies are expected to carry the *rationale*, not a restatement of the diff — what the defect was,
how it failed, and what evidence supports the fix. State the evidence class explicitly:

```
Verified: lock-free dotnet build Hecton8.Core.csproj - 0 errors, no warnings from this file.
No runtime/gameplay proof.
```

Do not delete explanatory comments during refactors. Rationale loss is treated as a regression —
comments documenting non-obvious mass-conservation or write-window guards have been silently dropped
before, and the code looked identical afterwards.

---

## Finding real defects

Two observations from an extended audit pass, offered because they were expensive to learn:

**Reachability first.** Before designing anything, confirm the code you are about to change actually
runs. In this repository, check the **`SignalBus<T>` signal type**, not the internal DTO or
`BufferID`. Grepping the DTO name nearly produced a false report that a fully-wired whalefall
pipeline was dead code — the external contract is the signal, and it had two live producers.

Two systems in-tree *are* genuinely unreachable and are worth either wiring or deleting:
`KelpForestVoxelSpawnJob` (zero references anywhere) and `KelpForestTrenchIntegrationBridge`
(a MonoBehaviour whose GUID appears in no scene, prefab or asset — check MonoBehaviour reachability
by GUID, since scenes bind by GUID and not by class name).

**Two defect classes recur here**, and both are cheap to grep for:

1. min/max folds seeded with a sentinel that can never lose
2. parameters accepted and then ignored — one caused every creature of a species in a biome to
   receive an identical genome, i.e. visually perfect clones, on a real code path

Directive-driven work has a poor hit rate against this codebase; most systems described as missing
turned out to be already implemented. Reading unfamiliar files in your own area has a far better
one. Verify the premise before implementing the fix.
