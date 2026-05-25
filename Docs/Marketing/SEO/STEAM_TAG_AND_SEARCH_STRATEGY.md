# HECTON-8 Steam Tag And Search Strategy

Status: pre-store-page operating plan
Owner lane: SHINOBU_81 / Steam positioning
Public stance: single-player-first
Runtime impact: none

## Source Boundary

Primary platform references:

- Steam Tags documentation: https://partner.steamgames.com/doc/store/tags
- Steam Visibility documentation: https://partner.steamgames.com/doc/marketing/visibility?language=english
- Steam UTM Analytics documentation: https://partner.steamgames.com/doc/marketing/utm_analytics
- Steam Localization documentation: https://partner.steamgames.com/doc/store/localization

Recheck these before final Steam page publish. Tags, visibility rules, and dashboard behavior are external platform facts.

## Hard Truth

Steam is not a social feed. Broad "look at our game" traffic is not enough. Steam visibility is driven by accurate tags, player behavior, purchases, language support, and the store matching the right audience. Wishlists matter for launch notifications and Popular Upcoming pressure, but they are not a magic algorithm switch.

The current goal is not "maximum reach". The goal is to make Steam understand the game correctly enough that the right cold viewer lands on the page and does not feel baited.

## Tag Weighting Rules

Steam's docs state that developers can set tags in the Tag Wizard, top tags carry visibility weight, and the top 20 influence store placement/similarity. Top 5 must describe the actual game clearly. Top 15 must remain relevant enough for filters.

HECTON-8 must not use tags that imply missing features:

- no multiplayer-mode tag;
- no `Online Co-Op`;
- no `Multiplayer`;
- no `MMO`;
- no `Open World Survival Craft` unless the actual playable loop proves open-world craft expectations;
- no `Procedural Generation` unless the public build has enough visible procedural proof;
- no `Simulation` if the public product is primarily authored survival and cinematic fake-first systems.

## Proposed Tag Stack V0

Use this only after the first playable route and first store screenshots exist.

| Rank | Tag | Why | Risk |
|---:|---|---|---|
| 1 | Survival | Core expectation: staying alive under pressure. | Must have resource/health/oxygen/pressure stakes visible. |
| 2 | Exploration | The audience expects unknown depths and routes. | Screenshots must show navigable spaces, not only interiors. |
| 3 | Base Building | High-intent Subnautica-adjacent audience. | Do not use if base loop is not playable in demo. |
| 4 | Sci-fi | NASA-punk, hardware, anomaly, Seed Ship. | Visuals must not read as generic horror corridor. |
| 5 | Singleplayer | Corrects multiplayer-scope boundary. | Low information; should not outrank real genre tags if Steam suggests otherwise. |
| 6 | Atmospheric | Deep sea noir, pressure, audio dread. | Needs real clips, not copy. |
| 7 | First-Person | If the first public build is first-person. | Remove if camera plan changes. |
| 8 | Underwater | High semantic fit if available in Tag Wizard/popular tags. | Verify current tag availability in Steamworks. |
| 9 | Horror | Use only if danger/dread is mechanically present. | Can repel cozy exploration players. |
| 10 | Survival Horror | Use only if the demo has sustained threat, not just mood. | Strong expectation of fear pacing and threat. |
| 11 | Resource Management | Useful for craft/survival loop. | Must not become tedious spreadsheet promise. |
| 12 | Crafting | Use if tool/base craft is central. | Avoid if crafting is lightweight. |
| 13 | Building | Secondary to `Base Building`. | Too broad; keep below stronger tags. |
| 14 | Immersive Sim | Only if systems genuinely support player problem-solving. | Dangerous if the build is linear. |
| 15 | Noir | Differentiates aesthetic if Steam supports it. | Niche tag; verify search value. |
| 16 | Story Rich | Use only if Seed Ship/lore route is demonstrable. | Bad if first demo is mechanics-only. |
| 17 | Physics | Use only if vehicle/heavy machinery physics are visible. | Do not imply full sim if using cinematic cheats. |
| 18 | Open World | Use only if route scale and nonlinearity prove it. | High expectation; risky early. |
| 19 | Indie | Low-information support tag. | Should not occupy high rank. |
| 20 | Early Access | Only when actual EA state applies. | Must match Steam EA rules and copy. |

## Alternative Tag Stacks

### If First Demo Is Exploration-Heavy

1. Exploration
2. Survival
3. Atmospheric
4. Sci-fi
5. Singleplayer
6. First-Person
7. Underwater
8. Base Building
9. Resource Management
10. Crafting

Use this if the first route sells spaces, machinery, pressure, and discovery better than base building.

### If First Demo Is Base/Engineering-Heavy

1. Base Building
2. Survival
3. Resource Management
4. Sci-fi
5. Singleplayer
6. Crafting
7. Exploration
8. Building
9. Atmospheric
10. First-Person

Use this only if the base-builder footage is not clunky and the UI does not require explanation.

### If First Demo Is Horror/Pressure-Heavy

1. Survival Horror
2. Atmospheric
3. Survival
4. Sci-fi
5. Singleplayer
6. First-Person
7. Exploration
8. Underwater
9. Psychological Horror
10. Resource Management

Use this if the first public clip produces fear comments without the caption saying "this is scary".

## Steam Search Keywords

Steam tags are the stronger platform-native signal, but store copy must still include natural search phrases. Do not keyword-stuff.

High-fit phrases:

- underwater survival;
- deep sea survival;
- sci-fi survival;
- base building survival;
- pressure survival;
- industrial underwater base;
- salvage survival;
- deep ocean horror;
- first-person survival;
- hostile ocean survival;
- machinery survival;
- anomaly beneath the ocean;
- single-player survival;
- NASA-punk survival;
- deep sea noir.

Forbidden or risky phrases:

- "Subnautica killer" public copy;
- "like Subnautica but better";
- "100km multiplayer";
- "zero stutter" without current profiler proof;
- "realistic fluid simulation" if the implementation is cinematic cheat-first;
- "infinite world" unless technically true in public build;
- "fully procedural" unless public route proves it.

## Store Copy Keyword Placement

| Store surface | Keyword role | Rule |
|---|---|---|
| Short description | 1 core phrase only | Must read like a pitch, not SEO paste. |
| Long description first paragraph | 2-3 natural phrases | Include `single-player`, `deep sea`, and `survival` if true. |
| Feature bullets | System nouns | Pressure, salvage, machinery, base systems, Seed Ship anomaly. |
| Screenshot captions | Specific visual proof | "pressure hatch", "salvage route", "flooded machinery bay". |
| Trailer title/description | Human search terms | "deep sea survival", not internal acronyms. |
| Steam announcements | Update-specific terms | "new base pressure test", "salvage route pass". |

## Competitor Overlap Without Looking Derivative

Players should understand the adjacency quickly, but the page must not read like a knockoff.

Allowed internal comparison:

- HECTON-8 targets survival, exploration, base systems, and undersea dread audiences.

Allowed public phrasing:

- "single-player deep-sea survival";
- "industrial survival below the light";
- "salvage, pressure, and machinery in a hostile ocean".

Forbidden public phrasing:

- "for fans of Subnautica" in headline copy;
- "better than Subnautica";
- "Subnautica but realistic";
- "Subnautica killer".

If a creator uses those comparisons independently, do not amplify them in official copy.

## Tag QA Before Publish

Before publishing tags, run this valid blind cold-reader test:

1. Use at least 10 readers who have not read the docs, pitch, target nouns, or tag rationale.
2. Show only the top 5 tags and capsule; do not explain the intended answer.
3. Record `context_exposure`; only `NONE` counts toward pass rate.
4. Ask what they think the game is.
5. If 2+ valid readers say "multiplayer underwater base builder", tags failed.
6. If 2+ valid readers say "horror walking sim", tags failed unless the demo is exactly that.
7. If "single-player sci-fi underwater survival with bases/exploration" is the dominant valid read, tags pass.

## Tag Drift Monitoring

After store page launch:

- check public tags weekly for inaccurate community tags;
- remove tags that imply unavailable features;
- record top 20 tag order in `Marketing/KPI` weekly report;
- compare Steam "More Like This" section monthly;
- if Steam starts associating the game with wrong genres, adjust top 5/top 15 before more outreach.

## First-Pass Tag Decision

Default V0 recommendation, subject to actual demo proof:

1. Survival
2. Exploration
3. Base Building
4. Sci-fi
5. Singleplayer
6. Atmospheric
7. First-Person
8. Underwater
9. Resource Management
10. Crafting

Do not lock this until screenshots exist. The first six real screenshots decide the tag stack, not design intent.
