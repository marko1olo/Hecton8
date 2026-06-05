# Rationale 2020

Evidence class: STATIC VERIFIED. Runtime status remains PENDING VERIFICATION.

Decision:
- Recommend one first-20 spine: shallow scenic salvage loop -> deterministic titanium/copper -> copper wire -> scanner.

Why:
- Current source has split authority: `FirstHourDirector` pushes copper/copper wire while `Quest_Graph` pushes titanium/scanner.
- Scanner is the first route-changing craft. Copper wire alone is too narrow for the product-facing first route.
- Copper remains necessary, but `CopperVein` must stay Drill-gated. First-20 copper needs an authored shallow source.
- Titanium field is Salvage-gated. If SalvageSampler is not assigned/unlocked, first titanium must come from an authored starter-compatible outcrop/cache.
- Existing scanner recipe requires `Comp_SensorPackage` and `Comp_CopperWire`; quest graph completion from titanium alone is false authority.
- Death rules exist architecturally, but critical first-hour item retention/recovery needs explicit penalty rules and tests.

Rejected:
- Changing CopperVein to a weaker tool gate.
- Leaving copper and titanium as independent mandatory first-hour owners.
- Claiming placement, oxygen return, scenic quality, or death recovery from static data.

Risk:
- If recipe owner is not declared before implementation, tests can pass against the wrong data source.
- If route placement is not authored, the repair remains spreadsheet-only.
