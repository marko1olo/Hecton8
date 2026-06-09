# P6802 Aegir Sky Window Machine Production Source

Release Set: `RS284_AEGIR_SKY_WINDOW_MACHINE`
Packet ID: `P6802_AEGIR_SKY_WINDOW_MACHINE`
Primary surface: `in_game_wiki`
Longform surface: `external_site`
Spoiler tier: `1`

## Purpose

This packet fills the gap between short sky-window primers and Black Keel recovery denial. The player should understand that Aegir's sky is a working route machine: moon shadows, storm ceiling, charged weather, tide load, radiation count, carrier phase, quarantine and tonne-window mass can disagree.

The article must not imply FTL, instant rescue, magical sky effects, or arbitrary refusal. A bright sky is a partial route state, not a promise.

## Source Path

- `Docs/Lore/AppliedContent/packets/RS284_AEGIR_SKY_WINDOW_MACHINE.packets.json`
- `Docs/Lore/AppliedContent/articles/RS284_AEGIR_SKY_WINDOW_MACHINE/<locale>_external_site.md`

## Runtime Path

Authoring source exports into:

- `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`
- `Docs/Lore/AppliedContent/in_game_wiki/<locale>/P6802_AEGIR_SKY_WINDOW_MACHINE.md`
- `Docs/Lore/AppliedContent/external_site/<locale>/P6802_AEGIR_SKY_WINDOW_MACHINE.md`
- `Assets/_SourceData/DataMonolith/Narrative/applied_lore_route_cards.csv`

Runtime must consume baked packet rows and packet hash only. It must not read this markdown, the source JSON, or the longform article files.

## Integration Intent

Bind `0x28055B25` to readable world objects that make sky state diegetic:

- `poi.aegir_sky_window_plate`
- `poi.radiation_count_strip`
- `poi.moon_shadow_tide_card`
- `poi.black_keel_window_timer`
- `poi.p63_relay_board`

The desired player-facing behavior is a route decision, not exposition: transmit, shelter, wait, hold ascent, or preserve proof until the sky machine stops contradicting itself.
