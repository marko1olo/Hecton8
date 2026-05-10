Date: 2026-04-16
Status: ARCHIVED

**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# HECTON-8 — Perf / QA / Release Workstream

Data: 2026-04-13  
Status: PENDING VERIFICATION

## Chto zakryvaet etot front

- Performance truth
- Memory truth
- Test coverage
- Build cadence
- Release hardening

## Pochemu eto nelzya otkladyvat

Na tekuschem obeme proekta ruchnaya pamyat komandy uzhe ne derzhit vsyu sistemu.  
13 testov dlya takogo proekta oznachayut slabuyu strahovku ot regressiy.

## Osnovnye zadachi

### Front A. Perf truth on target hardware

- CPU frame time.
- GC/frame.
- VRAM.
- RenderTexture memory.
- Batches / SetPass.
- Streaming hitch profile.

### Front B. Regression discipline

- Zafiksirovat obyazatelnye before/after zamery.
- Nelzya prinimat perf-fix bez chisel.
- Nelzya schitat ispravlenie zakrytym bez povtora stsenariya.

### Front C. Critical flow test coverage

- Main menu path.
- Save/load path.
- Core survival path.
- Pause/settings path.
- One narrative/progression path.

### Front D. Build validation

- Regulyarnye production builds.
- Progon smoke checklist.
- Logirovanie nereshennyh build blockers.

### Front E. Memory / render triage

- Texture memory.
- RT memory.
- Lighting/post cost.
- Scatter CPU cost.

## Candidate owners

- `Assets/_Project/Tests`
- performance-sensitive world owners
- build issue docs
- save/menu/pause critical path owners

## Do-Not-Touch Scope

- Ne rasshiryat gameplay scope.
- Ne prevraschat perf work v novyy feature work.
- Ne perepisyvat sistemy bez izmereniya.

## Kak drobit po agentam

Agent 1:
- perf numbers / profiling routines
- Zadacha: sobrat truth baseline.

Agent 2:
- critical flow tests
- Zadacha: podnyat minimalnuyu strahovku ot regressiy.

Agent 3:
- build smoke and issue ledger
- Zadacha: prevratit sborki v regulyarnyy kontrol, a ne sluchaynoe sobytie.

## Expected Result

- Poyavlyayutsya realnye tsifry.
- Regressii nachinayut lovitsya ranshe.
- Finalnaya dovodka perestaet idti vslepuyu.

## Exit Criteria

- Est baseline po perf/memory.
- Est smoke suite po critical path.
- Build blockers fiksiruyutsya regulyarno, a ne ot sluchaya k sluchayu.
