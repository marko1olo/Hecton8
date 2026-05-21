# AUP Precision Standards

Date: 2026-05-21
Status: PENDING VERIFICATION
Owner: SHINOBU_ARCHIVARIUS_SURGEON
Evidence class: STATIC_DOC / STATIC_SOURCE

## Authority

`AbsoluteUniversePosition` is the only simulation-scale position authority.

Observed source layout:

- integer grid sector: `GridX`, `GridY`, `GridZ`
- local offset: `LocalX`, `LocalY`, `LocalZ` or `float3 Local`
- total blit size: `48` bytes

`Transform.position` is presentation space only.

## Burst Distance Sequence

Required sequence:

1. Convert sector delta to `double3`.
2. Add local offset delta in double precision.
3. Perform distance comparison in double when correctness depends on large-world separation.
4. Cast the local delta to `float3` only after subtracting the observer or local origin.
5. Never cast absolute world coordinates to `float3`.

Allowed sketch:

```csharp
double3 deltaD = target.ToDoubleMeters() - observer.ToDoubleMeters();
double distanceSq = math.lengthsq(deltaD);
float3 localDelta = (float3)deltaD;
```

Rejected sketch:

```csharp
float3 a = (float3)target.ToDoubleMeters();
float3 b = (float3)observer.ToDoubleMeters();
float3 delta = a - b;
```

## Rebase Rules

- `HectonFloatingOrigin` owns origin shifts.
- Cached world-space floats are invalid across a rebase.
- Do not interpolate across a rebase boundary.
- Physics and gameplay systems must consume the owner pause/barrier route before reading or writing shifted state.
- Systems with crash relevance keep a 300-frame black-box ring or document why an owner-level ring covers them.

## Non-Claims

This file does not prove every source file obeys AUP. Use static scans and runtime replay before claiming compliance.
