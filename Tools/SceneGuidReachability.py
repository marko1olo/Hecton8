"""Answer "is this MonoBehaviour in a scene or prefab?" without lying about the binary scenes.

WHY THIS EXISTS
---------------
Four scenes in this project are binary-serialized, and one of them is the scene the player loads:

    Assets/_Project/Scenes/02_HECTON_WORLD.unity        6,270,260 B   <- the world
    Assets/_Project/Scenes/010_TEST.unity               5,810,980 B
    Assets/_Project/Scenes/020_RENDER_SANDBOX.unity    60,755,399 B
    Assets/_Project/Scenes/020_RENDER_SANDBOX_V2.unity  5,023,428 B

A binary scene does not begin with `%YAML` and contains the string `m_Script` zero times. A script reference
is stored as the GUID's raw bytes in NIBBLE-SWAPPED order, so `rg` for the hex string cannot match it.

On 2026-07-29 that cost a retracted headline finding. A whole investigation concluded "nine authoring buttons
were never pressed" and "crafting is unreachable" from

    rg -l "<guid>" --glob '*.unity' --glob '*.prefab' Assets

returning nothing. Re-tested with byte awareness, six of those types — Fabricator, BiomeMatrixDirector,
WorldContentSocket, WorldContentDirector, ScavengePopulator, WorldCaveDirector — are all present in
02_HECTON_WORLD.unity. The buttons had been pressed and the scene had been saved the whole time.

So this is not a convenience wrapper. It is the difference between a reachability answer and a coin flip, and
it belongs in a tool rather than in whoever happens to remember.

WHAT IT REFUSES TO DO
---------------------
It will not report a negative without first proving the search can find a positive. Pass --control with a GUID
you know is present (or accept the default) and the tool exits non-zero if that control is not found, rather
than telling you your type is absent. A search that cannot find what is there has not established that
anything is missing.

TWO TRAPS IT ENCODES, both hit during that same investigation
-------------------------------------------------------------
1. `Assets/_Recovery/` is gitignored, so ripgrep skips it by default while a plain filesystem walk does not.
   It holds full scene copies carrying live-looking m_Script references. A hit there is NOT reachability. This
   tool reports those separately instead of folding them into the answer.
2. A component carried by a prefab INSTANCE emits no type-tree entry unless overridden, so absence from a
   binary scene proves nothing about prefab-borne components. PlayerInventory reads zero type-name hits in the
   world scene and is demonstrably live via Player.prefab. The tool prints this caveat with every negative
   rather than leaving the reader to remember it.

WHAT A NEGATIVE HERE STILL DOES NOT MEAN
----------------------------------------
Absent from every scene and prefab is not the same as unreachable. A MonoBehaviour also reaches the game by
being constructed at runtime — GameBootstrapper holds 38 `AddComponent<>` calls and the domain installers hold
more. Always pair a negative from this tool with a search for `AddComponent<T>`, `new T()` and
`GetComponent<T>`, and split Editor-only construction from runtime construction. Two types looked dead by scene
search in that same session and were live: ConstructionManager and WorldProceduralProxyInstance.

USAGE
-----
    python -B Tools/SceneGuidReachability.py --type Fabricator
    python -B Tools/SceneGuidReachability.py --guid 65748c03d0baf8a4a95eca4dd9cfa4c4
    python -B Tools/SceneGuidReachability.py --type FaunaBrain --type HUDNotification
    python -B Tools/SceneGuidReachability.py --audit            # list which scenes are binary
    python -B Tools/SceneGuidReachability.py --self-test        # verify the tool against known answers

Exit codes: 0 answered, 1 control GUID not found (answer withheld), 2 bad input, 3 self-test failed.
"""

from __future__ import annotations

import argparse
import pathlib
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

REPO = pathlib.Path(__file__).resolve().parent.parent
ASSETS = REPO / "Assets"

# A type whose script GUID is known to sit in a live text scene. Every negative result is withheld unless the
# search finds this first, because a search that cannot find a known positive has proven nothing.
DEFAULT_CONTROL_TYPE = "WorldStreamingDirector"

# Gitignored, holds full scene copies. A hit here is not reachability.
NON_LIVE_DIR_PARTS = ("_Recovery", "DEPRECATED", "_Archive", "Archive")


def nibble_swap(raw: bytes) -> bytes:
    """Byte order a binary Unity scene stores a GUID in."""
    return bytes(((b & 0x0F) << 4) | (b >> 4) for b in raw)


def guid_of_type(type_name: str) -> str | None:
    """Script GUID from `<type>.cs.meta`, or None when the type has no single unambiguous .cs."""
    matches = [p for p in ASSETS.rglob(f"{type_name}.cs.meta")]
    if len(matches) != 1:
        return None
    for line in matches[0].read_text(encoding="utf-8-sig", errors="replace").splitlines():
        if line.strip().startswith("guid:"):
            return line.split(":", 1)[1].strip()
    return None


def load_scenes() -> tuple[list[tuple[pathlib.Path, bytes]], list[tuple[pathlib.Path, bytes]]]:
    """Every scene and prefab, split by the %YAML header test rather than by extension or by hope."""
    text: list[tuple[pathlib.Path, bytes]] = []
    binary: list[tuple[pathlib.Path, bytes]] = []
    for path in sorted(list(ASSETS.rglob("*.unity")) + list(ASSETS.rglob("*.prefab"))):
        try:
            data = path.read_bytes()
        except OSError:
            continue
        (text if data.lstrip()[:5] == b"%YAML" else binary).append((path, data))
    return text, binary


def is_live(path: pathlib.Path) -> bool:
    return not any(part in NON_LIVE_DIR_PARTS for part in path.parts)


def find(guid: str, text, binary) -> tuple[list[pathlib.Path], list[pathlib.Path]]:
    """Files containing the GUID, split live vs non-live. Text by string, binary by both byte orders."""
    live: list[pathlib.Path] = []
    other: list[pathlib.Path] = []
    lower, upper = guid.encode(), guid.upper().encode()
    raw = bytes.fromhex(guid)
    swapped = nibble_swap(raw)

    for path, data in text:
        if lower in data or upper in data:
            (live if is_live(path) else other).append(path)
    for path, data in binary:
        if swapped in data or raw in data:
            (live if is_live(path) else other).append(path)
    return live, other


def report(label: str, guid: str, text, binary) -> bool:
    live, other = find(guid, text, binary)
    print(f"\n{label}  guid {guid}")
    if live:
        print(f"  PRESENT in {len(live)} live scene/prefab file(s):")
        for path in live[:12]:
            kind = "binary" if (path, ) and path in {p for p, _ in binary} else "text"
            print(f"    {kind:6s} {path.relative_to(REPO).as_posix()}")
        if len(live) > 12:
            print(f"    ... and {len(live) - 12} more")
    else:
        print("  ABSENT from every live scene and prefab.")
        print("    NOT the same as unreachable: check AddComponent<T>, new T(), GetComponent<T> and split")
        print("    Editor-only construction from runtime. Two types looked dead this way and were live.")
        print("    Also: a component on a prefab INSTANCE emits no binary type-tree entry unless overridden,")
        print("    so absence from a binary scene does not settle prefab-borne components.")
    if other:
        print(f"  (plus {len(other)} hit(s) in non-live paths — recovery/archive copies, not reachability)")
        for path in other[:4]:
            print(f"    ignored {path.relative_to(REPO).as_posix()}")
    return bool(live)


def self_test(text, binary) -> int:
    """Verify the tool answers three known cases correctly. Establishes the search works before trusting it."""
    print("self-test")
    failures = 0

    binary_names = {p.name for p, _ in binary}
    expected_binary = {
        "02_HECTON_WORLD.unity",
        "010_TEST.unity",
        "020_RENDER_SANDBOX.unity",
        "020_RENDER_SANDBOX_V2.unity",
    }
    missing = expected_binary - binary_names
    if missing:
        print(f"  FAIL expected these to be binary and they are not: {sorted(missing)}")
        failures += 1
    else:
        print(f"  ok   the four known binary scenes are detected as binary ({len(binary)} binary total)")

    # A text-only search must MISS the world scene; the byte-aware search must HIT it. That contrast is the
    # entire reason this file exists, so it is the thing worth pinning.
    world = next((d for p, d in binary if p.name == "02_HECTON_WORLD.unity"), None)
    if world is None:
        print("  FAIL world scene not loaded")
        return 3
    fab = guid_of_type("Fabricator")
    if not fab:
        print("  SKIP Fabricator.cs.meta not resolvable")
    else:
        if fab.encode() in world:
            print("  FAIL the world scene contains the GUID as TEXT — the premise of this tool is wrong now")
            failures += 1
        elif nibble_swap(bytes.fromhex(fab)) not in world:
            print("  FAIL nibble-swapped GUID not found in the world scene")
            failures += 1
        else:
            print("  ok   text search misses the world scene, byte-aware search finds it")

    brain = guid_of_type("FaunaBrain")
    if brain:
        live, _ = find(brain, text, binary)
        if live:
            print(f"  FAIL FaunaBrain was expected absent everywhere, found in {len(live)}")
            failures += 1
        else:
            print("  ok   FaunaBrain absent from every scene and prefab, both encodings")

    print("SELF_TEST=" + ("PASS" if failures == 0 else "FAIL"))
    return 0 if failures == 0 else 3


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--type", action="append", default=[], help="MonoBehaviour type name")
    parser.add_argument("--guid", action="append", default=[], help="script GUID, 32 hex chars")
    parser.add_argument("--control", default=DEFAULT_CONTROL_TYPE, help="type known to be present")
    parser.add_argument("--audit", action="store_true", help="list binary scenes and exit")
    parser.add_argument("--self-test", action="store_true", help="verify the tool and exit")
    args = parser.parse_args()

    if not ASSETS.is_dir():
        print(f"Assets not found at {ASSETS}", file=sys.stderr)
        return 2

    text, binary = load_scenes()
    total = len(text) + len(binary)
    print(f"{total} scene/prefab files: {len(text)} text, {len(binary)} BINARY")

    if args.audit:
        for path, data in binary:
            print(f"  BINARY {len(data):>12,} B  {path.relative_to(REPO).as_posix()}")
        return 0

    if args.self_test:
        return self_test(text, binary)

    if not args.type and not args.guid:
        parser.print_help()
        return 2

    control_guid = guid_of_type(args.control)
    if control_guid is None:
        print(f"control type {args.control} has no unambiguous .cs.meta — cannot validate the search", file=sys.stderr)
        return 1
    control_live, _ = find(control_guid, text, binary)
    if not control_live:
        print(f"CONTROL {args.control} ({control_guid}) NOT FOUND — the search is broken, withholding answers",
              file=sys.stderr)
        return 1
    print(f"control {args.control} found in {len(control_live)} file(s) — search validated")

    for type_name in args.type:
        guid = guid_of_type(type_name)
        if guid is None:
            print(f"\n{type_name}: no single unambiguous {type_name}.cs.meta — pass --guid instead")
            continue
        report(type_name, guid, text, binary)

    for guid in args.guid:
        cleaned = guid.strip().lower()
        if len(cleaned) != 32:
            print(f"\n{guid}: not 32 hex chars", file=sys.stderr)
            continue
        report(cleaned, cleaned, text, binary)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
