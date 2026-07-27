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
plausible-looking output. Two real examples: vegetation whose bioluminescent pulse could never fire,
and ecosystem biome derived from a coordinate hash entirely decorrelated from the visible seafloor.
Both compiled, ran, and logged nothing.

**If a system can silently collapse, write a probe that fails loudly instead.**
`EcosystemGeologyBiomeLanes.SampleLaneDistribution` + `H8_GeologyBiomeLaneProbe` are the reference
pattern: the canonical mapping lives in one shared unit that both the runtime and the audit call, so
the audit cannot drift and report health while the runtime is degenerate.

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
