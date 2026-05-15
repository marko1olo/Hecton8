# HECTON-8 Lore Binary Package

Status: BAKED / VERIFY BEFORE HANDOFF
Owner: NARRATIVE_LORE_STREAMING_BAKER

## Artifact

- Blob: `Data/Lore/Encyclopedia.h8bin`
- Manifest: `Data/Lore/Encyclopedia.manifest.json`
- Source directory: `Docs/Lore`
- Current source: `Docs/Lore/Lore_Bible.md`

## Binary Layout

Little-endian.

- Header: `<4sHHIIIIII>`, 32 bytes.
- Magic: `H8LR`.
- Version: `1`.
- Record: `<IQI>`, 16 bytes.
- Record fields: `uint32 hash`, `uint64 absolute_offset`, `uint32 compressed_length`.
- Payload: zlib-compressed UTF-8 Markdown.
- Alignment: every payload starts on a 16-byte boundary.
- Hash: `H8DataHash.ComputeFnv1A32` compatible FNV-1a over repository-relative ASCII path, A-Z folded to lowercase.
- Source validation: every Markdown payload must decode as UTF-8 before compression; invalid bytes fail the bake.

## Required Verification

Run before packaging:

```powershell
python Tools\VerifyLore.py --bake --check --list
python -B -m unittest Tools.test_verify_lore -v
```

The CLI anchors relative paths to the repository root, so the same commands work when launched from a subdirectory. Blob, manifest, and extracted Markdown writes use `.tmp` + atomic replace.

Expected active record:

```text
0xD1880394 Docs/Lore/Lore_Bible.md
```

To extract by path:

```powershell
python Tools\VerifyLore.py --source-path Docs\Lore\Lore_Bible.md --output-text .codex-artifacts\NARRATIVE_LORE_STREAMING_BAKER_latest_extract.md
```

Do not place operator notes under `Docs/Lore`; every Markdown file there is source content for the blob.
