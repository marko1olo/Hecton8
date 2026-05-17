# Habitat Pressure Budget - SHINOBU Mapping

Evidence boundary: STATIC_SOURCE / PYTHON_OFFLINE. Not Unity runtime proof.

## Inputs

- JSON: `Data/Habitat/HabitatPressureBudget.json`
- Binary: `Data/Habitat/HabitatPressureBudget.h8bin`
- Binary layout: `Data/Habitat/HabitatPressureBudget_BinaryLayout.json`
- Runtime owner: `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs`
- Import path should be a cold bake into the static database or a generated binary table. Do not parse JSON in gameplay ticks.

## Runtime Formula

```csharp
float ambientPressureMpa = (101325f + (1025f * 9.80665f * depthMeters)) * 0.000001f;
float totalSip = baseSipSum + lithiumReinforcementCount * 10f;
float stress = totalSip > 0f ? ambientPressureMpa / totalSip : 999f;
bool collapseStarts = stress > 1f;
float bowingMeters = math.min(0.1f, stress * stress * 0.1f);
```

This matches the batch directive: `Stress = AmbientPressure / Total_SIP`.

## Data Contract

- `hashFnv1a32`: unsigned 32-bit FNV-1a over the module stable ID.
- `hashFnv1a32Signed`: signed int32 view for C# table keys if required.
- `sip.baseSip`: MPa-equivalent scalar after physical pressure estimate and gameplay modifiers.
- `physics.designCrushPressureMPa`: min(elastic buckling, hoop yield) after safety and knockdown factors.
- `physics.crushDepthM`: hydrostatic depth where design pressure is exceeded.
- `runtimeStress.maxBowingBeforeRuptureM`: 0.1m hard deformation limit before rupture.

## Cylinder Crush Physics

- Elastic shell buckling: `Pcr = 2E/sqrt(3(1-nu^2))*(t/r)^3`.
- Hoop-yield limit: `Py = yieldStrength * t / r`.
- Ring presentation base: `baseHz=sqrt(YoungsModulusPa/densityKgM3)/(2*pi*radiusM)/max(4,lengthM/radiusM+8)`.
- The lower pressure is multiplied by material buckling knockdown, joint efficiency, length knockdown, and divided by safety factor.
- This is a deterministic scalar pressure budget, not a finite-element shell solver.
- Beer-Lambert, Dalton, and Sabine are not authored by this baker; no optical, gas-partial-pressure, or reverb LUT is generated here.

## Test Scenario

- Scenario: 1000m base with four glass corridors.
- Depth: 1000.0m.
- Ambient pressure: 10.153141 MPa.
- Total SIP before reinforcement: -12.0.
- Stress before reinforcement: 40.612565.
- Collapse seconds: 0.25.
- Lithium reinforcements needed: 3.
- Stress after reinforcement: 0.564063.

## Binary Contract

- Magic: `H8HPB`.
- Endianness: `little` (`<` struct prefix, marker 0x01020304).
- Header: 64 bytes.
- Module records: offset 64, count 15, stride 96 bytes.
- God-mode records: offset 1504, count 15, stride 80 bytes.
- Total binary size: 2704 bytes; all offsets and strides are 16-byte aligned.
- Lookup contract: `stateless_binary_search_by_hash`; module and God-mode records are sorted by FNV-1a hash so runtime can binary-search without a private index.

## Scalability

- Low/Toaster: consume `toasterData.records` or binary module records only: FNV key, base SIP, crush-depth integer, fixed-point stress, collapse flag.
- Middle: shader wetness/crack alpha uses stress and clamps bowing at 0.1m.
- High: richer pressure groan, crack decals, and per-module shader stress from same scalar data.
- Ultra/RTX-overkill: consume `godModeVisualData.records` for high-res pressure gradients, shell harmonics, and harmonic noise. Gameplay collapse remains the scalar SIP threshold.

## Rejected Runtime Work

- No Unity joints.
- No runtime finite-element deformation.
- No mesh collider rebuild.
- No new EventID for this data import.
- No JSON parsing in `Tick`, `FixedTick`, `Update`, or Burst jobs.

STATUS: STRESS MATH BAKED / VERIFIED MASTER GRADE - PYTHON_OFFLINE; PENDING UNITY IMPORT VERIFICATION
