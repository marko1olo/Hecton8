# 2203 Texture Intake Checklist

Evidence class: STATIC VERIFIED. This checklist does not imply Unity import, material binding, or runtime proof.

## Intake States

- `SOURCE_ONLY`: downloaded or generated source exists, but no passing audit/visual review.
- `REJECT`: hard blocker. Do not import, derive final PBR, or bind to Unity materials.
- `CANDIDATE`: static audit has no hard issue, but human visual review, channel plan, or derivation proof is incomplete.
- `READY_FOR_DERIVATION`: static audit passes, 2x2/3x3 visual review passes, prompt/source role is documented, and channel derivation route is explicit.

## Required Files Per Candidate

- Source image under `Docs/GeneratedAssets/Gemini/Outputs/Batch22/`.
- Sidecar manifest beside source.
- Audit CSV and Markdown.
- 2x2 tile preview.
- Contact sheet when auditing more than one source.
- Status label in manifest.

## Static Audit Command

```powershell
python Tools/GeminiTextureIntakeAudit.py --project-root . --root Docs/GeneratedAssets/Gemini/Outputs/Batch22/[FILE].png --out-dir Docs/GeneratedAssets/Gemini/Audit/Batch22/[TARGET]
```

## Visual Tile Inspection

Reject if:

- left/right or top/bottom edge seam is visible in 2x2;
- inner wrap band is visible even if exact edge pixels match;
- 3x3 preview exposes repeated hero shell, stone, vein, crack, coral cup, or foam stamp;
- diagonal dune/ripple/band pattern repeats at route scale;
- border, frame, watermark, text, logo, labels, UI, horizon, object render, or scene perspective appears;
- albedo carries baked directional shadow, cast shadow, studio highlight, bloom, or beauty-render lighting;
- surface/photic material is darkened into abyss/noir to hide weak detail.

## Script Metric Gates

`Tools/GeminiTextureIntakeAudit.py` currently checks:

- square image;
- source size and power-of-two warnings;
- lossy JPG warning;
- LR/TB edge mismatch;
- LR/TB 8-pixel band mismatch;
- luminance mean/min/max;
- black/white clipping percentage;
- channel saturation percentage;
- rough role classification from filename.

Current hard static issues include non-square source, severe seam/band mismatch, albedo too dark for surface/shallows, clipped albedo, and saturated channel data.

Do not edit the script unless a narrow proven bug exists. No bug was proven in this task.

## Do Not Import Checklist

Do not import to Unity when any item is true:

- manifest says `SOURCE_REFERENCE_ONLY`, `STATIC_REJECTED`, `UNITY_MATERIAL_BLOCKED`, or equivalent;
- audit verdict is `REJECT`;
- source has visible seams, edge bands, or hero repetition;
- albedo has baked lighting, clipped range, or crushed black/white patches;
- texture is a scenic photo/render rather than an orthographic material sample;
- only one image exists and no channel-specific derivation plan exists;
- PBR role is ambiguous or channels are planned from the same image without derivation proof;
- no 2x2 preview exists;
- no sidecar manifest exists.

## Derivation Candidate Checklist

Before marking `READY_FOR_DERIVATION`:

- source is square, power-of-two preferred, 1024 minimum for world material source;
- static audit has no hard blocker;
- visual 2x2 and 3x3 tile inspection passes;
- prompt/source role is recorded;
- meters-per-tile is recorded;
- albedo source has no baked lighting;
- height source is grayscale or explicitly extractable;
- roughness/wetness/AO plan follows material truth;
- metallic remains black for sand, shell, coral, algae, foam, salt, and basalt except localized ore/mineral contracts;
- MRAO packing contract is named before Unity import;
- texture size/compression expectation is written for compact, middle, high, and ultra lanes.

## PBR Channel Rules

- Albedo: base color only, sRGB, no lighting/shadows/highlights.
- Height: grayscale relief source for cracks, pores, shell fragments, foam cell depth, coral cups, or grain structure.
- Normal: derive from accepted height/source; use OpenGL tangent-space for Unity route unless shader contract says otherwise; BC5 where supported.
- Roughness: material-state logic, not constant gray; wet basalt smoother, salt/sediment rough, algae/biofilm variable, shells/calcite mostly matte.
- AO: cavity-biased only; not random dirt over exposed planes.
- MRAO: R metallic, G roughness/smoothness per shader manifest, B AO, A wetness/emission/family mask.
- Emission: not for these photic substrate targets unless a later bioluminescent organism target explicitly owns it.

## Target-Specific Rejection

Wet basalt:

- reject giant teal veins, black abyss grade, chrome wetness, blob rock, baked shiny highlights.

Basalt cyan veins:

- reject neon circuit lines, full-rock metallic, repeated vein hero shapes, glow pretending to be mineral.

Sand/shell:

- reject beige mud, diagonal dunes, repeated shells/stones, beach-photo perspective, baked shell shadows.

Foam/salt:

- reject opaque white strip, dirty storm foam default, wave photo perspective, repeated bubble stamps.

Caustics:

- reject scenic underwater render, fish/diver/terrain content, bloom glare, global abyss caustic look, harsh grid/knot repetition.

Algae/coral tint:

- reject candy reef, random neon, black abyss coral, wallpaper cup repetition, flat color wash.

## Hardware Expectations

- Compact: preserve material identity at 512-1024 imported world resolution with compression and mips; caustic/decal masks may be 256-512. No ugly mode.
- Middle: 1024-2048 key photic materials, clearer masks, stronger local decals.
- High: 2048 hero surfaces, richer normals and wetness/foam layers.
- Ultra: 4096 source/bake archives and hero-only detail where streaming/profiler proof allows; no gameplay truth changes.

## Proof Labels

- This checklist and prompt pack: `STATIC VERIFIED`.
- Any future Gemini download before audit: `SOURCE_ONLY`.
- Any future source with hard audit issue: `STATIC_REJECTED`.
- Any future source after audit but before Unity preview: no runtime label, still not material proof.
- Unity material preview requires a later explicit owner slot and current screenshot/profiler evidence when runtime claims are made.
