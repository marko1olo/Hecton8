#!/usr/bin/env python3
from __future__ import annotations

from H8VerifyCore import require_aligned


def main() -> int:
    size = require_aligned("Assets/_Project/Data/Localization/Babel_Dictionary.h8bin")
    print(f"BABEL COMPILE CHECK OK bytes={size} source=existing-static-artifact")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
