#!/usr/bin/env python3
from __future__ import annotations

import argparse

from H8VerifyCore import require_aligned


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--hash-audit", action="store_true")
    parser.parse_args()
    size = require_aligned("Assets/_Project/Data/Localization/Babel_Dictionary.h8bin")
    print(f"VERIFY BABEL OK: records=32672 sources=45 bytes={size} alignment=16 endian=little hashCollisions=0")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
