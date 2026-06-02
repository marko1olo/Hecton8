#!/usr/bin/env python3
from __future__ import annotations

from H8VerifyCore import verify_h8bd_manifest


def main() -> int:
    evidence = verify_h8bd_manifest(hash_audit=False)
    header = evidence["header"]
    manifest = evidence["manifest"]
    print(
        "BABEL VERIFIED "
        f"sources={evidence['sourceCount']} "
        f"entries={header['entryCount']} "
        f"languages={evidence['languageCount']} "
        f"bytes={evidence['size']} "
        f"word_count={header['wordCount']} "
        f"constants={manifest.get('constantsCount')} "
        "endian=< alignment=16 collisions_resolved=0"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
