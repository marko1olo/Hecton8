# LOG_MARKET_RESEARCH

2026-05-23
Initialized external gamedev/community intelligence pass.
What was wrong: no local batch prompt or concrete agent ID was supplied; using MARKET_RESEARCH as explicit working ID.
What was done: read AGENTS.md, domain boundary, and relevant mandate files. Created status/rationale/log artifacts.
Cinematic Cheats used: none in runtime; analysis will evaluate competitors through visual-fake-first lens.
Exact Microseconds saved: 0, research-only.

2026-05-23
External gamedev/community intelligence report.

What was wrong:
- The question "are many using agents?" cannot be answered from Reddit, 4chan, or Dvach with a valid percentage. Those sources are sentiment and workflow evidence only.
- The underwater-survival lane is not empty. As of May 2026, Subnautica 2 is the category monarch, and several smaller titles occupy adjacent underwater horror/submarine/co-op niches.
- Public "AI-made game" framing is reputationally dangerous. Communities distinguish useful AI tooling from asset slop, shortcut fantasies, and broken vibe-coded systems.

What was done:
- Read HECTON-8 authority docs, domain boundary, and relevant mandates: QA_Evidence_Text_Filter_Audit, OPT_Cinematic_Cheat_Protocol_Visual_Fake_First, OPT_Performance_Budgets_FrameTime_VRAM_Limits, REND_Shader_Noir_Aesthetics_Dithering_Fog, ARCH_Global_Registry_ServiceLocator_DI_Init.
- Collected current sources from GDC 2026, Unity AI docs, Reddit r/Unity3D/r/gamedev/r/vibecoding, 4chan /vg/agdg + /g/gedg + /g/vcg, Dvach /gd/ + /v/ + /ai/, Steam pages, and recent games press.
- Delegated three read-only OSINT passes: Reddit Unity/Gamedev, 4chan/Dvach, competitor landscape. Integrated their findings with direct verification.

AI-agent adoption:
- Strong numeric baseline: GDC 2026 reports 36% of game industry professionals using generative AI at work; 30% among game-studio respondents; use is higher in publishing/support/marketing/PR. Common uses: research/brainstorming, daily/admin tasks, code assistance, prototyping. Source: https://gdconf.com/article/gdc-2026-state-of-the-game-industry-reveals-impact-of-layoffs-generative-ai-and-more/
- Sentiment baseline: the same GDC report says 52% of respondents view generative AI as negative for the industry, while only about 7% view it as positive. That is not anti-tool absolutism; it is a hard reputational warning.
- Agent-specific baseline: no reliable broad percentage found. Observed evidence says agents are present and normalizing, but not default production infrastructure for most gamedev teams.
- Unity AI is now a real workflow competitor, not a distant concept. Unity's official page lists Agentic Assistant, AI Gateway, and MCP Server, with project-aware editor/IDE integration, permissions, generated-asset metadata, and third-party-agent support. Sources: https://unity.com/features/ai and https://support.unity.com/hc/en-us/articles/48060149523476-Getting-started-with-Unity-AI-open-beta-user-guide
- Official Unity requirements are not perfectly aligned across surfaces: Unity page says Unity 6.0+, support article says Unity 6.3+. Treat adoption as PENDING VERIFICATION for any exact project integration requirement.

Reddit read:
- r/Unity3D has direct reports of daily Unity AI use for animator/controller wiring, MCP-in-IDE workflows, asset generators, profiler triage, and rapid prototypes. The useful pattern is narrow editor work plus specs and project docs, not autonomous full-game creation. Source: https://www.reddit.com/r/Unity3D/comments/1tdex9f/has_anyone_actually_been_using_unity_ai_curious/
- r/Unity3D also shows harsh pushback against generic Unity-specific AI agents when they appear to reinvent existing engine systems or hide maintenance costs. Source: https://www.reddit.com/r/Unity3D/comments/1n0wfb2/built_an_ai_agent_to_speed_up_unity_workflows/
- r/vibecoding's gamedev discussion matches the technical risk profile: AI helps snippets, prototypes, debug/test scaffolding, and routine work; full systems remain risky because games have weaker conventions, harder visual feedback, and fewer unit-testable truths. Source: https://www.reddit.com/r/vibecoding/comments/1pn8qsp/how_is_vibe_coding_for_game_development/
- r/gamedev has live burnout evidence where AI accelerated work but also added refinement/debug/redesign burden and cohesion debt. Source: https://www.reddit.com/r/gamedev/comments/1t8exkc/im_quitting_game_development/
- Practical conclusion: Reddit will accept AI if it removes tedious wiring, generates disposable prototypes, or helps profiler/debug work. It will punish AI as a marketing claim, especially for key art/assets and "dream game shortcut" messaging.

4chan read:
- /vg/agdg is active: current Amateur Game Development General had hundreds of posts on 2026-05-22 and contains Demo Days, resources, progress posting, Steam/itch concerns, Godot questions, art bottleneck complaints, and AI-as-idea/tool talk. Source: https://boards.4chan.org/vg/thread/567846506
- /g/gedg is more engineering-heavy: current thread discusses engine development, softbody physics, broadphase choices, level tools, RenderDoc/resources, and one concrete AI-profiler/debug usage report. Source: https://boards.4chan.org/g/thread/108848278
- /g/vcg is explicit AI-agent culture: Codex, Claude Code, local models, quotas, multi-agent ideas, hallucination checks, and free/paid setup debates. Source: https://boards.4chan.org/g/thread/108864733
- /v/ signal is hostile to weak pitch language and shallow crafting. Subnautica remains a strong mental model: depth, exploration, fear, and curiosity matter more than recipe tables. Sources include https://boards.4chan.org/v/thread/739369302 and active Subnautica/Dvach links below.
- Practical conclusion: chans are useful for early detection of contempt. They will attack "AI slop", generic procedural worlds, UE/engine marketing, and empty crafting. They reward build proof, visual identity, specific mechanics, and ruthless progress posts.

Dvach read:
- /gd/ is alive and concrete. Direct JSON showed about 498 current threads. Active visible anchors include Godot #80, Unreal Engine #28, Two Weeks Games, screenshot/progress threads, Steam/Google Play sales questions, project logs, narrative-design questions, and "vibe-gamedev" / asset-generation discussion. Sources: https://2ch.org/gd/ and examples https://2ch.org/gd/res/1088293.html, https://2ch.org/gd/res/1088506.html, https://2ch.org/gd/res/1014584.html
- /v/ had an active Subnautica 2 thread with gameplay/content/quality discussion around the Early Access build. Source: https://2ch.org/v/res/10324802.html
- /ai/ had an active "Agents and vibe-coding #6" thread on 2026-05-21 with concrete concerns about subscriptions, local models, tool crashes, Docker/git isolation, and destructive agent mistakes. Source: https://2ch.org/ai/res/1616724.html
- Practical conclusion: Russian community signal is not "everyone uses agents." It is "agents are known, discussed, and distrusted; practical users care about isolation, cost, local models, and not losing work."

Competitor map:
- Critical direct: Subnautica 2. Released Early Access 2026-05-14; Steam describes underwater survival adventure, alien world, 4-player co-op, bases, crafting, exploration. Steam showed very positive reviews and massive current review volume. PC Gamer reported 4M sales in under one week. Sources: https://store.steampowered.com/app/1962700/Subnautica_2/ and https://www.pcgamer.com/games/survival-crafting/bad-news-for-krafton-subnautica-2-broke-4-million-copies-sold-in-less-than-a-week/
- High direct/future: Full Fathom. Submarine, survival, maintenance, sonar/navigation/turrets, scavenging, threats, demo, release TBA. Source: https://store.steampowered.com/app/2302470/Full_Fathom/
- High adjacent horror: Dark Mass. Fully submerged manor psychological horror, fall 2026, underwater movement/physics, narrative/puzzles/stalker. Source: https://www.gematsu.com/2025/03/underwater-psychological-horror-game-dark-mass-announced-for-ps5-xbox-series-and-pc
- High attention/streamer: Murky Divers. Up to 8-player underwater co-op horror, blind submarine/sonar, oxygen, procedural wrecks, proximity voice, voice-mimic AI disclosure. Source: https://store.steampowered.com/app/2963880/Murky_Divers/
- Medium-high systemic reference: Barotrauma. 2D co-op submarine survival horror/RPG, machinery, leaks, weapons, pumps, class roles, modding, very strong review base. Source: https://store.steampowered.com/app/602960/Barotrauma/
- Medium direct/future: EXONAUTIS. Q4 2026 planned; underwater sci-fi survival on oceanic exoplanet, biomes, creatures, vehicles, modular base-building. Source: https://store.steampowered.com/app/3474620/EXONAUTIS/
- Medium adjacent: Cthulhu: The Cosmic Abyss, Iron Lung, SOMA, Under The Waves, Narcosis, Dredge/Sunless-style maritime horror, Pacific Drive/Forever Skies vehicle-as-lifeline survival. These compete for tone, dread, vehicle maintenance, or exploration attention, not always for mechanics.

Strategic read for HECTON-8:
- Do not position as "underwater survival." That lane is occupied and Subnautica 2 has a current mass-market gravity well.
- Position as "deep-sea industrial/NASA-punk systemic survival horror." The hook must be machinery, pressure, blackout telemetry, sonar/signal dread, environmental hostility, deterministic failure cascades, authored abyssal spaces, and noir lighting/audio.
- Combat should not be the headline. Agency should be: tools, avoidance, decoys, repairs, lockdowns, sonar discipline, power routing, pressure management, hull failures, emergency procedures.
- Crafting must not become "collect trash to unlock trash tier 2." Crafting should exist as expedition preparation, repair logistics, emergency improvisation, and route commitment.
- Procedural generation is not a selling point by itself. Author-controlled biome identity, silhouettes, threat staging, and readable exploration beats matter more.
- AI agents are an internal production multiplier only if bounded by mandates, route ownership, compile/profiler gates, and review loops. They are a public liability if sold as the reason the game is good.
- Devlog posture: show playable proof, profiler captures, black-box telemetry, before/after environment shots, authored hazards, and exact mechanics. Avoid engine wars, AI boast posts, and abstract "simulation depth" claims.

Cinematic Cheats used:
- Runtime: none; research-only.
- Strategic cheat recommendation: buy belief with authored fog, sonar ghosts, pressure audio, diegetic UI, deterministic VFX, and shader/noir fakes before spending runtime budget on physical truth. Use saved budget for high-tier visual overkill, not for invisible simulation.

Regression model:
- CPU/GC/memory/cadence/correctness: no runtime change, no compile required, no Unity verification claimed.
- Research risk: chan/Dvach content is volatile and anecdotal; Steam/review counts change; Subnautica 2 numbers are as observed/reported around 2026-05-22/23.
- Residual risk: no paid market telemetry, no Steam wishlist scrape, no Discord/YouTube/TikTok sentiment scrape, no Russian VK/TG sweep.

Exact Microseconds saved:
- 0. Research-only. Any runtime performance implication is strategic guidance, not measured proof.
