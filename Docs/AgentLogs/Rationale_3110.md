# Rationale 3110 - Lore / World Consistency

Evidence class: `STATIC_SOURCE`, `STATIC_DOC`.
Runtime status: `PENDING VERIFICATION`.

## Mandates Followed

- `PROG_Quest_State_Graph_Logic.txt`
- `CORE_Abyss_Survival_Systems_O2_Pressure_Logic.txt`
- `DATA_Inventory_Resources_Items_SOA_Layout.txt`
- `DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `QA_Evidence_Text_Filter_Audit.txt`
- `ARCH_Signal_Lane_Segregation.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

## Decisions

- Treat lore/world work as route evidence and object function, not exposition wallpaper.
- Keep CopperVein Drill-gated. Weakening it to Knife/Any would violate tool progression and is explicitly rejected by `ContentSanityValidator`.
- Prefer `Data_FiberKelp -> Comp_FiberMesh -> Comp_PressureSeal` as the 3110 V0 reroute while the starter drill route is missing.
- Keep PressureSeal static-only until a real route target consumes it and save/load restores that changed state.
- Do not accept `01_ORBIT` in product flow until root scene-flow conflict is resolved.
- Do not use text to compensate for failed surface/water/Aegir visuals.

## Rejected Alternatives

- Copper-only V0: rejected because Drill route is missing and first route must be more spectacular than a resource proof.
- Downgrade CopperVein tool gate: rejected by validator and tool bible.
- Silica -> GlassPanel as 3110 preferred route: rejected for now because `Comp_GlassPanel` is not accepted by `FirstHourDirector` and lacks a stronger route repair function in current evidence.
- Text-only repair explanation: rejected because gameplay/world bibles require visible physical state change.

## Low / Middle / High / Ultra Consequences

- Low: FiberKelp route keeps bright shallow readability, simple harvest, short labels, and one visible seal target without dense optional lore.
- Middle: adds clearer route cues, pinger trail, service buoy, and localized scanner/PDA short forms.
- High: adds richer environmental contradiction layers and better repair feedback without changing route truth.
- Ultra: adds optional black-box/archive depth and secondary sensor evidence after the same harvest/craft/repair state is proven.
