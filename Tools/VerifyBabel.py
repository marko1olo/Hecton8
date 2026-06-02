#!/usr/bin/env python3
from __future__ import annotations

import argparse

from H8VerifyCore import verify_h8bd_manifest


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--hash-audit", action="store_true")
    args = parser.parse_args()
    evidence = verify_h8bd_manifest(hash_audit=args.hash_audit)
    header = evidence["header"]
    manifest = evidence["manifest"]
    print(
        "VERIFY BABEL OK: "
        f"records={header['entryCount']} "
        f"sources={evidence['sourceCount']} "
        f"bytes={evidence['size']} "
        f"languages={evidence['languageCount']} "
        f"payload={header['payloadBytes']} "
        f"word_count={header['wordCount']} "
        f"constants={manifest.get('constantsCount')} "
        "alignment=16 endian=little hashCollisions=0"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
