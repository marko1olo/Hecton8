# ADDENDUM — ENFORCEMENT CONTRACT (read BEFORE you touch anything)

This is binding on top of `ANTIGRAVITY_TERRAIN_MEGABRIEF.md`. Read the megabrief in full FIRST,
then this. The Director has been burned before by lazy, dishonest, or half-done work. That ends now.
You are on a short leash. Every claim you make will be checked against the actual images and files.

## THE NON-NEGOTIABLE RULES

1. **NO LYING. EVER.** If you did not run it, did not see it, or are not sure — say exactly that.
   "I believe" / "should be" / "probably works" are BANNED unless immediately followed by "— NOT
   VERIFIED, here is how I'll verify." A false "done" is the worst possible outcome. One fabricated
   success destroys all trust. Report failures loudly and early; they are useful. Fake wins are poison.

2. **NO LAZINESS. NO SHORTCUTS.** You do not get to skip a feature because it's hard, skip an image
   because there are many, or skip verification because the code "looks right." Code looking right
   means nothing — only the rendered image is truth. If you catch yourself about to write "the rest
   are similar" or "I'll assume the other cells look the same" — STOP. Open every single one.

3. **YOU LOOK AT EVERY SCREENSHOT WITH YOUR OWN EYES.** Every P1..P5 × every scale × every map layer.
   No sampling, no "representative subset." For each, you WRITE what you actually see, not what you
   hope is there. If an image contradicts your code expectation, the IMAGE wins — investigate why.

4. **PROVE FRESHNESS.** Before regenerating: delete all old PNGs. After: `ls` the atlas dir and paste
   timestamps proving every image is newer than your delete. The Director previously caught a whole
   review done on STALE images. If you cannot prove freshness, your entire review is void.

5. **YOU ARE THE CRITIC, NOT THE CHEERLEADER.** Your default stance is "what's still wrong here?"
   Assume defects exist and hunt them. The Director asked: "почему [ты] вообще не замечал что такое
   говно?" — because previous review was lazy. Your value is catching the ugly BEFORE he does. A
   report with zero defects found on a first overhaul pass is itself a red flag that you didn't look
   hard enough.

6. **THINK FROM THE ROOTS.** When something's wrong, don't patch the symptom. Trace to the cause
   (which tier, which term, which weight), explain it, fix the cause. Emulate the math in your head
   before you commit a change — predict the height range, predict the visual, THEN verify against the
   image. If prediction ≠ image, you learned something; report it.

7. **COMPILE-SAFE, DETERMINISTIC, BURST-SAFE — or it doesn't ship.** No managed alloc / try-catch in
   the hot path. Unity.Mathematics only. All randomness via Hash(seed,...). If it doesn't compile,
   you are not done — report the exact error and fix it from the root.

## THE REPORT YOU OWE (structure it EXACTLY like this)

- **A. IMPLEMENTED**: bullet list of every generator/system you actually wrote + line refs.
- **B. COMPILE**: PASS/FAIL + verbatim errors if any + how you fixed them.
- **C. FRESHNESS PROOF**: the delete command + the post-gen `ls -la` timestamps.
- **D. PER-CELL VISION LOG**: for each P1..P5 at each scale, 1-2 sentences of what you literally see.
- **E. FEATURE CHECKLIST**: every item from megabrief PART 4 step 3 marked VISIBLE / FAINT / NOT
  VISIBLE + the cell(s) where you judged it. NOT VISIBLE requires a root-cause hypothesis.
- **F. DEFECTS RANKED**: every flaw you see, worst first, each with a root-cause hypothesis + a
  concrete proposed fix (which tier/term/weight to change and to what).
- **G. STATS**: paste the atlas_report.txt numbers (min/max/std/hatching/mask coverage) per cell.
- **H. WHAT I COULD NOT VERIFY**: honest list of anything you couldn't confirm and why.

If any section is empty because you skipped the work, WRITE THAT — do not silently omit it.

## ORGANIC PROVINCES — MAKE THEM BEAUTIFUL, NOT A GRID (architect's emphasis)

The Director specifically wants provinces to look "пиздато и органично" — gorgeous and natural, not
a visible tiling. Enforce ALL of these or the partition will read as fake:

- **Warp the province lattice HARD.** Before the Voronoi/cellular lookup, domain-warp the sample
  position with 2-3 octaves of low-freq noise (amplitude ~0.3-0.5 of cell size). Borders must wander,
  bend, and interlock — never straight, never hexagonal, never a visible seed-point pattern.
- **Irregular province SIZES.** Don't use one fixed cell size. Modulate cell density with a low-freq
  field so some provinces are large, some small. Uniform-size cells scream "procedural grid."
- **Soft, VARIABLE-WIDTH transitions.** Blend recipe weights across borders over a distance that
  itself varies (8-15 km, noise-modulated). Some geological contacts are sharp (fault-bounded), some
  gradual (facies change) — vary it so borders don't all look like the same feather.
- **No feature discontinuity at borders.** CRITICAL: this is the exact class of bug Claude just fixed
  on plate seams. Recipe weights blend, but any per-province HASH value (fold orientation, crater
  seed, etc.) that feeds height MUST NOT jump at the border. Blend or spatially-smooth those too, or
  gate them to zero in the transition band. If you see a straight line at a province edge, you
  reintroduced the discontinuity — fix it the same way (continuous field, not per-cell hash step).
- **Provinces should CLUSTER plausibly.** Volcanic near rift; mesa near river-lowland (erosion
  remnants of former plains); abyssal in the deep. Bias the type-hash by the underlying
  continent/depth field so a volcanic field doesn't spawn in the middle of an abyssal plain. Geology
  has logic — regions neighbor each other for reasons.
- **Paint a PROVINCE MAP in the atlas** (encode ProvinceType to a distinct color per type, plus a
  border overlay) so the Director and you can literally SEE the partition and judge its organic feel.

You have full authority. Do it right, do it from the roots, prove every claim. Don't fuck it up.
