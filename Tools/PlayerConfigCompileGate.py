"""Compiles a Unity assembly in PLAYER configuration, with UNITY_EDITOR undefined.

Why this exists. Nothing else in the project can see a whole class of defect: an
`#if UNITY_EDITOR` region opened too wide, swallowing members that unguarded runtime code
calls. The Editor always defines UNITY_EDITOR. Unity's generated .csproj compiles in editor
mode. `Directory.Build.props:10` and `Directory.Build.targets:39,128` inject
UNITY_EDITOR;UNITY_EDITOR_WIN on top. So `dotnet build` is green while the player build is
broken, and stays green indefinitely.

Run against Hecton8.Core on 2026-07-27 it reported 71 errors in 13 files - every one a real
break, and five of them in shapes the static scanner
(`Tools/EditorGuardLeakScanner.py`) structurally cannot detect: guarded FIELDS rather than
methods, and partial classes split across files.

This is the ground truth. The static scanner is the fast pre-filter; when the two disagree,
this wins.

How it works, and why it needs no edits to shared build files. MSBuild global properties -
anything passed with `-p:` - cannot be reassigned from inside a project. Every
`<DefineConstants>$(DefineConstants);UNITY_EDITOR</DefineConstants>` in the Directory.Build
files therefore becomes a no-op once DefineConstants arrives as a global property. The
define list is rebuilt here from the csproj plus HectonUnityCliVersionDefines, with the
three editor tokens removed. Semicolons must be escaped as %3B or MSBuild reads them as
property separators.

Usage:
    python -B Tools/PlayerConfigCompileGate.py
    python -B Tools/PlayerConfigCompileGate.py --assembly Hecton8.Core
    python -B Tools/PlayerConfigCompileGate.py --also-editor   compile both configurations
    python -B Tools/PlayerConfigCompileGate.py --print-defines only show the define set

Exit code 1 when the player configuration fails to compile.

Caveat kept explicit: this is a semantic compile of one assembly in player define
configuration. It is NOT a player build, and it does not prove IL2CPP, stripping, platform
behaviour, or anything at runtime. It proves exactly one thing - that the C# in this
assembly resolves with UNITY_EDITOR undefined.
"""

import argparse
import os
import re
import subprocess
import sys

EDITOR_TOKENS = ("UNITY_EDITOR", "UNITY_EDITOR_WIN", "UNITY_EDITOR_64")
UNITY_ROOT = r"C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Data"


def _first_match(path, pattern):
    with open(path, encoding="utf-8", errors="replace") as handle:
        found = re.search(pattern, handle.read())
    return found.group(1) if found else ""


def player_defines(csproj, props="Directory.Build.props"):
    """Rebuild the csproj define set without the editor tokens."""
    tokens = []
    raw = _first_match(csproj, r"<DefineConstants>([^<]*)</DefineConstants>")
    if os.path.exists(props):
        raw += ";" + _first_match(
            props, r"<HectonUnityCliVersionDefines>([^<]*)</HectonUnityCliVersionDefines>"
        )
    # Hecton8.Core picks this up from Directory.Build.targets:128, which we are neutralising.
    raw += ";HECTON_CORE_CONTRACTS_DLL_LEGACY"
    for token in raw.split(";"):
        token = token.strip()
        if not token or token in tokens or token in EDITOR_TOKENS:
            continue
        tokens.append(token)
    return tokens


def compile_assembly(assembly, defines=None, unity_root=UNITY_ROOT):
    """Compile one assembly. defines=None means the project's own editor configuration."""
    dotnet = os.path.join(unity_root, "DotNetSdk", "dotnet.exe")
    command = [
        dotnet,
        "build",
        f"{assembly}.csproj",
        "-t:Rebuild",
        f"-p:UnityEditorManagedDir={os.path.join(unity_root, 'Managed')}",
        "-v:minimal",
        "--nologo",
    ]
    if defines is not None:
        # %3B or MSBuild splits the value into separate properties.
        command.append("-p:DefineConstants=" + "%3B".join(defines))
    finished = subprocess.run(command, capture_output=True, text=True, errors="replace")
    return finished.stdout + finished.stderr


ERROR_RE = re.compile(
    r"^(?P<file>[A-Za-z]:\\[^(]+)\((?P<line>\d+),(?P<col>\d+)\): (?:error|ошибка) (?P<code>CS\d+)"
)


def parse_errors(output):
    """One entry per unique (file, line, column, code).

    MSBuild emits every diagnostic twice - once per target pass - so dedupe is required or
    the count doubles. The column is part of the key: two distinct errors can share a line
    and a code, and dropping the column silently undercounted 71 as 65.
    """
    seen = {}
    for raw in output.splitlines():
        found = ERROR_RE.match(raw.strip())
        if not found:
            continue
        key = (
            found.group("file"),
            int(found.group("line")),
            int(found.group("col")),
            found.group("code"),
        )
        seen.setdefault(key, raw.strip())
    return [
        {"file": file, "line": line, "column": column, "code": code, "message": message}
        for (file, line, column, code), message in seen.items()
    ]


def _report(errors, detail=False):
    by_file = {}
    for entry in errors:
        by_file.setdefault(entry["file"], []).append(entry)
    for path, entries in sorted(by_file.items(), key=lambda item: -len(item[1])):
        short = path.split("Scripts" + os.sep, 1)[-1]
        codes = sorted({entry["code"] for entry in entries})
        print(f"  {len(entries):3d}  {short}  [{', '.join(codes)}]")
        if not detail:
            continue
        # The count alone names the file but not the broken symbol, which is the one fact needed to
        # move the guard boundary. The message was already captured in parse_errors and used to be
        # discarded on the way out.
        for entry in sorted(entries, key=lambda item: (item["line"], item["column"])):
            trimmed = entry["message"].split("): ", 1)[-1]
            print(f"        {entry['line']}:{entry['column']}  {trimmed}")


def runtime_assemblies():
    """Every first-party assembly that ships in a player: no .Editor, no .Tests."""
    found = []
    for name in sorted(os.listdir(".")):
        if not name.startswith("Hecton8.") or not name.endswith(".csproj"):
            continue
        stem = name[: -len(".csproj")]
        lowered = stem.lower()
        if ".editor" in lowered or ".tests" in lowered:
            continue
        found.append(stem)
    if "Hecton8.Core" in found:
        found.remove("Hecton8.Core")
        found.insert(0, "Hecton8.Core")
    return found


def sweep_runtime(unity_root, detail=False):
    """Compile every runtime assembly in player configuration. Returns total error count."""
    total = 0
    for assembly in runtime_assemblies():
        defines = player_defines(f"{assembly}.csproj")
        errors = parse_errors(compile_assembly(assembly, defines, unity_root))
        total += len(errors)
        marker = "OK  " if not errors else "FAIL"
        print(f"  [{marker}] {assembly:<32} {len(errors)} player errors")
        if errors:
            _report(errors, detail)
    print(f"\nALL RUNTIME ASSEMBLIES: {total} player-configuration errors")
    if total:
        print("A player build cannot be produced until these resolve.")
    return total


def main():
    # The compiler speaks the OS display language, so on a Russian-locale host the diagnostics come
    # back as Cyrillic plus U+FFFD replacement characters from the subprocess decode, and printing
    # them to a cp1251 console raises UnicodeEncodeError before a single error is shown. Degrade the
    # unprintable characters instead of losing the whole report.
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(errors="replace")
        except (AttributeError, ValueError):
            pass

    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--assembly", default="Hecton8.Core")
    parser.add_argument("--unity-root", default=UNITY_ROOT)
    parser.add_argument("--also-editor", action="store_true", help="compile both configurations")
    parser.add_argument("--print-defines", action="store_true", help="show the define set and exit")
    parser.add_argument(
        "--all-runtime",
        action="store_true",
        help="sweep every first-party runtime assembly instead of just one",
    )
    parser.add_argument(
        "--detail",
        action="store_true",
        help="print every error's line, column and message, not just the per-file count",
    )
    args = parser.parse_args()

    if args.all_runtime:
        print("PlayerConfigCompileGate: sweeping all runtime assemblies with UNITY_EDITOR undefined")
        return 1 if sweep_runtime(args.unity_root, args.detail) else 0

    csproj = f"{args.assembly}.csproj"
    if not os.path.exists(csproj):
        print(f"PlayerConfigCompileGate: {csproj} not found. Run from the repo root.")
        return 2

    defines = player_defines(csproj)
    if args.print_defines:
        print(f"{len(defines)} defines, editor tokens removed:")
        print(";".join(defines))
        return 0
    for token in EDITOR_TOKENS:
        assert token not in defines, f"{token} leaked into the player define set"

    print(f"PlayerConfigCompileGate: compiling {args.assembly} with UNITY_EDITOR undefined...")
    player_errors = parse_errors(compile_assembly(args.assembly, defines, args.unity_root))

    if args.also_editor:
        print(f"PlayerConfigCompileGate: compiling {args.assembly} in editor configuration...")
        editor_errors = parse_errors(compile_assembly(args.assembly, None, args.unity_root))
        print(f"\nEDITOR configuration: {len(editor_errors)} errors")
        _report(editor_errors, args.detail)

    print(f"\nPLAYER configuration: {len(player_errors)} errors")
    if not player_errors:
        print(f"{args.assembly} compiles with UNITY_EDITOR undefined.")
        return 0
    _report(player_errors, args.detail)
    print(
        "\nEach of these is a member or field referenced from code that is not inside the same\n"
        "preprocessor guard as its declaration. Fix by moving the guard boundary, not by\n"
        "guarding the call site, unless the feature really is editor-only.\n"
        "NOT a player build: this proves C# resolution only, not IL2CPP, stripping or runtime."
    )
    return 1


if __name__ == "__main__":
    sys.exit(main())
