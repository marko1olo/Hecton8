# 2003 Kelp/Rock Dry-Land Placement Repair Spec

Batch worker: 2003  
Scope: static repair specification only. No Unity, no MCP, no imports, no build, no active Assets edits.  
Primary audit incorporated: `Docs/Reports/Batch20/WORLD_PROCEDURAL_SCATTER_DRY_LAND_RISK_AUDIT_20260604.md`

## Authorities Read

- `AGENTS.md`
- `VISION_LOCKS.md`
- `PROJECT_BIBLES.md`
- `world.md`
- `terrain.md`
- `water.md`
- `PROCEDURAL_ASSET_PIPELINE.md`
- `3DMODEL_FLORA_CORAL.md`
- `3DMODEL_GEOLOGY_ROCKS.md`
- `performance.md`
- `TASTE.md`
- `.agents-skills/REND_Instanced_Flora_Physics.txt`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

`Docs/Actual Domains of Project.txt` was checked and returned no readable content in this workspace.

## Static Finding

Dry-land leakage is credible and sourced in the current static data/code path.

Evidence:

- `WorldProceduralFieldSampler.cs` writes `DepthMeters = math.max(0f, input.WaterSurface - input.CenterHeight)` and secondary sampling repeats `math.max(0f, waterSurface - seafloorHeight)`. Terrain above water therefore resolves to depth `0`, not a separate dry state.
- Underwater or seafloor-attached rule assets with `minDepthMeters: 0` can pass the depth gate on dry terrain unless another gate rejects them.
- `WorldProceduralPlacementRule.requiredSubstrate` defaults to `Any`. Serialized target rules commonly omit it, so the runtime value is `Any`.
- `PassesStrictSubstrateEnvelope` returns true for `RequiredSubstrate == None` or `Any`, so `Any` is not strict for kelp/coral/rock grounding.
- `preferSeafloor` is serialized as `0` on inspected kelp, coral, rock, and safe-pocket rules. In `WorldProceduralPlacementRule`, that means false/no preference.
- `WorldProceduralScatterDirector.MatchesScatter` applies preferred biome, zone, and socket checks only when `!runtimeRule.StrictEnvelopeMapping`. Default `strictEnvelopeMapping` is true, so the strict path currently bypasses these preferred filters.
- `ResolveRuntimeVariant` falls back to `ProxyOnly` variants when a final-ready variant is not preferred or selected. All inspected target families allow proxy primitives and include proxy-only variants.

## Highest Risk Rules

Depth-zero dry-risk rules:

- `rule.kelp.starter`
- `rule.kelp.tall`
- `rule.kelp.patch.dense`
- `rule.kelp.canopy`
- `rule.coral.reef`
- `rule.coral.branching`
- `rule.coral.low`
- `rule.rocks.floor`
- `rule.rocks.cluster`
- `rule.pocket.safe`

Related anchor/proxy-risk rules:

- `rule.kelp.abyssal`
- `rule.coral.massive`
- `rule.coral.plate`
- `rule.coral.brittle`
- `rule.rocks.arch`
- `rule.rocks.shelf`

These second-group rules already have positive minimum depths, but still need explicit substrate/seafloor intent and proxy blocking for production-visible routes.

## Ownership Boundaries

- Dry versus submerged truth belongs to the field sampling/scatter eligibility route, not BioForge and not mesh builders.
- Kelp/coral/rocks/pockets placement thresholds belong to `WorldProceduralPlacementRule` assets.
- Candidate rejection belongs to `WorldProceduralScatterDirector` and `WorldProceduralScatterDirectorEnvironmentalEnvelope`.
- Substrate truth is consumed through the vegetation bridge and `ScatterCandidateEvaluator`, but `Any` cannot remain accepted as strict substrate for ground-attached underwater domains.
- BioForge and mesh builders are editor-only final asset generators. They should not be used as placement eligibility proof.

## Required Repair

### 1. Add a hard submerged gate for underwater ground-attached domains

Later owner should add an allocation-free static helper in the scatter director path:

```csharp
private static bool RequiresSubmergedSeafloorPlacement(WorldPrefabFamilyProfile.ProceduralDomain domain)
{
    return domain == WorldPrefabFamilyProfile.ProceduralDomain.Kelp ||
           domain == WorldPrefabFamilyProfile.ProceduralDomain.Plant ||
           domain == WorldPrefabFamilyProfile.ProceduralDomain.Coral ||
           domain == WorldPrefabFamilyProfile.ProceduralDomain.Rock ||
           domain == WorldPrefabFamilyProfile.ProceduralDomain.RockCluster ||
           domain == WorldPrefabFamilyProfile.ProceduralDomain.RockArch ||
           domain == WorldPrefabFamilyProfile.ProceduralDomain.RockShelf ||
           domain == WorldPrefabFamilyProfile.ProceduralDomain.SafePocket ||
           domain == WorldPrefabFamilyProfile.ProceduralDomain.ResourcePocket ||
           domain == WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket;
}
```

Then reject candidates for these domains when effective depth is below a domain/rule floor. Do not let `GlobalQualityWeight` lower this truth gate.

Proposed minimum submerged depth floors:

- Kelp/coral shallow ground flora: `1.5m`
- Seafloor rock floor/cluster scatter: `1.0m`
- Safe/resource/hazard pocket: `2.0m`
- Existing deeper rules keep their higher authored `minDepthMeters`

If the project needs dry shoreline rocks or intertidal decoration, create separate shoreline/domain-specific rules instead of allowing seafloor rock rules to accept dry land.

### 2. Fix strict mapping inversion

Preferred biome, zone, and socket filters must run for strict mapping. Current checks are guarded by `!runtimeRule.StrictEnvelopeMapping`; this is opposite of the asset default intent.

Owner should change the runtime match path so preferred filters are enforced when strict mapping is true and arrays are non-empty. If non-strict legacy scoring is still required, split that behavior into a separate score/affinity path rather than using the strict hard gate branch.

### 3. Reject `Any` substrate for strict underwater ground-attached domains

For kelp, plant, coral, underwater rock, and pocket domains, `RequiredSubstrate == Any` must not satisfy strict substrate envelope checks. Treat missing/Any substrate as invalid authoring unless the route is explicitly marked as shoreline/intertidal.

Minimum recommended rule intent:

- Kelp: `requiredSubstrate = Rock`
- Coral: `requiredSubstrate = Rock`
- Rock floor/cluster/shelf/arch: `requiredSubstrate = Rock`
- Safe pocket: `requiredSubstrate = Any` only if combined with hard submerged gate; otherwise split into sand pocket and rock pocket rules

### 4. Set `preferSeafloor` on seafloor-attached rules

All inspected kelp, coral, rock, and safe-pocket rules currently serialize `preferSeafloor: 0`. Set it to true for underwater ground-attached routes. If any rule is intended to float or socket to walls only, give it a separate rule name and domain.

### 5. Block proxy variants on production-visible scatter

Production-visible kelp/coral/rocks/pockets must not resolve to proxy-only primitives. Later owner should:

- Set `allowProxyPrimitives: 0` on target production families after final-ready variants are verified.
- In runtime variant selection, reject proxy-only variants for production-visible underwater domains when final variants exist.
- Keep proxy use limited to editor diagnostics or explicit low-trust development flags, not `GlobalQualityWeight`.

## Rule Data Changes for Later Owner

Apply these in Unity or via reviewed asset patch by the Unity owner, not from this worker:

- `rule.kelp.starter`: `minDepthMeters 0 -> 1.5`, `preferSeafloor 0 -> 1`, `requiredSubstrate -> Rock`, add preferred shallow/photic biome and zone filters matching `rule.kelp.tall`.
- `rule.kelp.tall`: `minDepthMeters 0 -> 1.5`, `preferSeafloor 0 -> 1`, `requiredSubstrate -> Rock`.
- `rule.kelp.patch.dense`: `minDepthMeters 0 -> 1.5`, `preferSeafloor 0 -> 1`, `requiredSubstrate -> Rock`.
- `rule.kelp.canopy`: `minDepthMeters 0 -> 1.5`, `preferSeafloor 0 -> 1`, `requiredSubstrate -> Rock`.
- `rule.kelp.abyssal`: keep `minDepthMeters 700`, set `preferSeafloor 1`, `requiredSubstrate -> Rock`.
- `rule.coral.reef`: `minDepthMeters 0 -> 1.5`, `preferSeafloor 0 -> 1`, `requiredSubstrate -> Rock`, add explicit preferred biome/zone filters.
- `rule.coral.branching`: `minDepthMeters 0 -> 1.5`, `preferSeafloor 0 -> 1`, `requiredSubstrate -> Rock`.
- `rule.coral.low`: `minDepthMeters 0 -> 1.5`, `preferSeafloor 0 -> 1`, `requiredSubstrate -> Rock`.
- `rule.coral.massive`: keep `minDepthMeters 6`, set `preferSeafloor 1`, `requiredSubstrate -> Rock`.
- `rule.coral.plate`: keep `minDepthMeters 18`, set `preferSeafloor 1`, `requiredSubstrate -> Rock`.
- `rule.coral.brittle`: keep `minDepthMeters 900`, set `preferSeafloor 1`, `requiredSubstrate -> Rock`.
- `rule.rocks.floor`: `minDepthMeters 0 -> 1.0`, `preferSeafloor 0 -> 1`, `requiredSubstrate -> Rock`, add explicit preferred biome/zone filters or split shoreline rocks into a separate dry shoreline rule.
- `rule.rocks.cluster`: `minDepthMeters 0 -> 1.0`, `preferSeafloor 0 -> 1`, `requiredSubstrate -> Rock`, add explicit preferred biome/zone filters or split shoreline rocks into a separate dry shoreline rule.
- `rule.rocks.arch`: keep `minDepthMeters 40`, set `preferSeafloor 1`, `requiredSubstrate -> Rock`.
- `rule.rocks.shelf`: keep `minDepthMeters 20`, set `preferSeafloor 1`, `requiredSubstrate -> Rock`.
- `rule.pocket.safe`: `minDepthMeters 0 -> 2.0`, `preferSeafloor 0 -> 1`, keep or split substrate only after owner decides whether safe pockets can be sand-rooted. Runtime hard-submerged gate is mandatory either way.

## Risk Gates

Before owner lands the repair:

- Static check: no production underwater ground-attached kelp/coral/rock/pocket rule has `minDepthMeters == 0`.
- Static check: no production underwater ground-attached rule relies on omitted/default `requiredSubstrate`.
- Static check: no target production family keeps `allowProxyPrimitives: 1` unless explicitly marked editor-only or development-only.
- Code check: strict mapping does not disable preferred biome/zone/socket filters.
- Code check: dry terrain above water cannot be represented as valid submerged seafloor by depth `0`.
- Runtime visual proof by Unity owner: coastline, surface, photic shallows, and medium-depth routes retain dense premium ecology; fix must not delete shallow life to hide leakage.
- Runtime proof by Unity owner: final variants, not primitive proxies, appear on visible kelp/coral/rock/pocket routes.
- Performance proof by Unity owner: no managed allocations or hot scene searches in the placement gate.

## Quality Scaling Consequences

- Low: same dry/submerged truth gates; density, cadence, and LOD may reduce, but final visible ecology must remain non-placeholder.
- Middle: same truth gates; standard final variants and authored biome envelopes.
- High: same truth gates; increased legal density/capacity and richer final variant mix.
- Ultra: same truth gates; visual overkill through density, lighting, mesh variety, and material fidelity only. No proxy fallback and no dry leakage.

`GlobalQualityWeight` may scale fidelity, cadence, capacity, and optional telemetry. It must not change placement truth ownership, DTO layout, save identity, or authority route.

## Black Box Note

This worker produced a static spec only. If the later owner changes runtime scatter eligibility, the scatter system is a critical world system and should either extend its existing telemetry or add a fixed-size last-300-frame high-level candidate rejection buffer for NaN/crash dumps. Do not add per-candidate managed logging in the hot path.

## Non-Confirmation

No Unity execution, import, MCP call, scene load, prefab edit, or build was performed for this task.
