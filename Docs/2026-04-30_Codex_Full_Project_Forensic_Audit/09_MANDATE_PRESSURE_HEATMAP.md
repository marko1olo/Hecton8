# Mandate Pressure Heatmap

Status: PENDING VERIFICATION

Purpose:
- quantify where the codebase is under the most pressure from its own mandates
- show which domains are most likely to violate architecture/performance rules simply because of scale and pattern mix

## Static Signals By Folder

Columns:
- `Instance`: singleton / static authority residue signal
- `UpdateLike`: `Update` / `LateUpdate` / `FixedUpdate` surface
- `Coroutine`: `StartCoroutine` / `yield return`
- `Complete`: job barrier pressure
- `Registry`: `GlobalRegistry` coupling
- `Native`: native-container surface
- `Burst`: Burst usage
- `Addr`: Addressables/load-release surface

| Folder | Instance | UpdateLike | Coroutine | Complete | Registry | Native | Burst | Addr | Pressure read |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| `World` | 17 | 0 | 0 | 41 | 199 | 1181 | 47 | 47 | highest overall pressure; massive native/jobs surface plus barrier density |
| `Gameplay` | 11 | 9 | 0 | 7 | 215 | 142 | 5 | 5 | heavy gameplay coupling and moderate legacy-loop residue |
| `UI` | 4 | 3 | 0 | 1 | 191 | 21 | 1 | 12 | large registry-coupled runtime layer; player-facing complexity is high |
| `Core` | 0 | 4 | 0 | 10 | 59 | 88 | 5 | 3 | backbone is real but barrier and loop authority are concentrated here |
| `Construction` | 1 | 0 | 0 | 5 | 38 | 82 | 3 | 3 | respectable native depth with service-authority drift |
| `Audio` | 1 | 0 | 0 | 1 | 23 | 103 | 13 | 0 | strong native/Burst concentration in a small file set |
| `Fauna` | 0 | 0 | 0 | 2 | 17 | 156 | 3 | 0 | serious simulation lane, smaller than world but still risky |
| `Visor` | 1 | 0 | 0 | 0 | 33 | 9 | 0 | 28 | feature lane leans on registry and Addressables more than raw native jobs |
| `Interaction` | 0 | 4 | 0 | 2 | 22 | 34 | 2 | 0 | still has loop-style runtime touches in a hot domain |
| `Tools` | 6 | 0 | 36 | 2 | 30 | 22 | 1 | 0 | verification/tooling layer carries large coroutine residue |
| `Dev` | 0 | 0 | 35 | 0 | 7 | 0 | 0 | 0 | smoke/verifier layer is coroutine-heavy and intentionally non-pure |

## Interface Reality By Folder

Columns:
- `ITick`
- `IUpd`
- `IFixed`
- `ISlow`
- `ISave`
- `IPool`
- `IInteract`
- `Smoke`

| Folder | ITick | IUpd | IFixed | ISlow | ISave | IPool | IInteract | Smoke | Reading |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| `Gameplay` | 28 | 33 | 6 | 8 | 6 | 2 | 12 | 0 | gameplay owns a large share of cadence and interaction |
| `UI` | 48 | 38 | 0 | 6 | 0 | 2 | 0 | 0 | UI is not passive; it is deeply tick-driven |
| `World` | 27 | 9 | 2 | 22 | 4 | 1 | 1 | 0 | world prefers slow/background cadence, which fits the domain |
| `Construction` | 3 | 3 | 2 | 7 | 0 | 9 | 2 | 0 | pooling and slow-tick shape are strong here |
| `Audio` | 3 | 3 | 0 | 3 | 0 | 0 | 0 | 0 | compact but heavily active subsystem |
| `Quest` | 0 | 1 | 0 | 0 | 1 | 0 | 0 | 0 | quest runtime is present but relatively compact |
| `PDA` | 2 | 1 | 0 | 0 | 3 | 0 | 0 | 0 | PDA side has real persistence and some tick ownership |
| `Dev` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 92 | project relies heavily on bespoke runtime smoke/verifier surfaces |

## Interpretation By Mandate

### Zero-GC Hot Path Mandate

Most threatened by:
- `World`
- `Gameplay`
- `UI`
- `Core`

Reason:
- those are the biggest cadence owners
- they combine registry calls, native memory, and some legacy loop usage

### Jobs / Burst Mandate

Strongest adoption:
- `World`
- `Audio`
- `Fauna`
- `Construction`

Most at risk of wasting the benefit:
- `World`
- `Core`
- `Construction`

Reason:
- `.Complete()` density is highest where the systems are already the heaviest

### Anti-Singleton / Registry Purity Mandate

Most drift:
- `World`
- `Gameplay`
- `UI`
- `Tools`
- specific core services already called out by Archivarius drift docs

Reason:
- project has a real registry, but singleton residue remains widespread

### No-Coroutine Gameplay Mandate

Biggest residue clusters:
- `Tools`
- `Dev`

Interpretation:
- much of the coroutine surface is in verifier/smoke tooling rather than core gameplay
- that is better than gameplay-wide coroutine dependence, but it still indicates uneven mandate culture

### Verification Maturity

Best evidence:
- many bespoke smoke/verifier classes exist

Weak truth:
- formal automated test surface is still tiny

Interpretation:
- the project prefers custom runtime smoke infrastructure over stable automated regression suites

## Highest Pressure Domains

### 1. World

Why:
- largest code mass
- largest native surface
- largest Burst concentration
- highest barrier count
- heavy registry coupling

Verdict:
- strongest implementation depth
- strongest regression pressure

### 2. UI

Why:
- huge file count and line count
- surprisingly large tick ownership
- strong registry coupling

Verdict:
- healthier than average game UI engineering
- still too active to be treated as harmless presentation code

### 3. Gameplay

Why:
- heavy tick ownership
- high registry coupling
- meaningful legacy-loop residue

Verdict:
- core gameplay is real
- also one of the main architecture-drift carriers

### 4. Core

Why:
- dispatcher/backbone authority
- barrier density
- native container concentration

Verdict:
- strongest architecture value
- also one of the most dangerous places to get wrong

## Brutal Reading

The codebase does not have one dominant failure mode.

It has four:
- oversized world ownership
- active UI complexity
- gameplay coupling drift
- core authority overload
