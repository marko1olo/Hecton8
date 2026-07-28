"""Find duplicate C# method signatures before paying for a Unity compile.

WHY THIS EXISTS. An agent's edit was interrupted mid-refactor and left two
`ValidateTopology` methods with identical parameter types in
`ModuleArchitect1712.cs`, which fails CS0111 and takes down the whole
`Hecton8.Editor` assembly - and with it every editor tool and content generator
in the project.

I checked that file and told its author it was clean, on the strength of balanced
braces (135/135), balanced parens (869/869), and a grep for duplicate method
signatures that returned nothing. The brace counts were right. The duplicate scan
was worthless, because the regex was anchored to a single line and the real
declaration was written across five:

    private static void ValidateTopology(
        List<Vector3> vertices,
        List<Vector3> normals,
        List<Vector2> uvs,
        List<int> indices)

A structural check that cannot see a multi-line signature is not a structural
check. Only the compiler caught it, two minutes of Unity startup later.

This normalises whitespace across line breaks first, so a signature is compared
by its shape rather than its formatting, and it runs in well under a second.

SCOPE - READ THIS BEFORE TRUSTING IT WIDE.

Trustworthy on a SINGLE FILE that someone just edited. That is the case it was built
for and it is proven: on the real defect it reported
`ValidateTopology(List<Vector3>,List<Vector3>,List<Vector2>,List<int>)` at lines
1176 and 1209 in under a second, and a designed probe confirms it ignores the same
signature written inside a normal string, inside a multi-line verbatim string, and
inside a comment, while NOT flagging a legitimate 3-parameter overload and NOT
collapsing `ReadOnlySpan<char>` against `ReadOnlySpan<byte>`.

NOT trustworthy project-wide, measured: 104 hits across 3775 files on a tree that
compiles. The dominant remaining class is PREPROCESSOR-GUARDED ALTERNATE
DECLARATIONS - e.g. `ScatterDiagnosticsTracker.cs` declares the same class twice,
once with real bodies and once with `return default;` stubs, in mutually exclusive
`#if` branches. Both are legal; only one compiles. Resolving that needs the active
define set, which is not knowable from the text.

So do not wire this in as a blocking build gate. A gate with 104 false positives on
a healthy tree is one nobody reads and someone eventually deletes - the exact
`[FORBID] Self-check cascade` failure `AGENTS.md` names. Point it at the file an
agent just touched.

USAGE
    python -B Tools/cs_duplicate_member_check.py <file>

Exits 1 if any duplicate is found.
"""

from __future__ import annotations

import os
import re
import sys

# Matches a method declaration up to its opening paren, tolerating any amount of
# whitespace and newlines inside the parameter list. Deliberately loose on the
# return type: matching every generic and array form exactly is how a checker ends
# up with the same blind spot it was written to remove.
_DECL = re.compile(
    r"(?P<mods>(?:public|private|protected|internal|static|virtual|override|sealed|"
    r"abstract|extern|unsafe|async|new|partial|readonly)\s+)+"
    r"(?P<ret>[A-Za-z_][A-Za-z0-9_<>\[\],\.\s\?]*?)\s+"
    r"(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*"
    r"(?P<generics><[^(){};]*>)?\s*"
    r"\((?P<params>[^;{}]*?)\)"
    # Require a BODY. Without this the scan counts declarations that define nothing -
    # interface members, abstract methods, and partial-method signatures all end in `;`
    # and can legally repeat, which was the bulk of a 108-hit false-positive run on a
    # project that compiles. An optional `where` clause and a `=> expression` body are
    # both real definition forms and must still match.
    r"(?P<tail>\s*(?:where\s+[^({;]*?)?\s*(?:\{|=>))",
    re.MULTILINE | re.DOTALL,
)

# Contextual keywords and operators that take parentheses and are not method
# declarations. `sizeof(int)` appearing four times in one file is not four
# definitions of a method called sizeof, but the first version reported it as such.
_NOT_A_METHOD = frozenset((
    "if", "for", "foreach", "while", "switch", "catch", "lock", "using", "return",
    "new", "get", "set", "add", "remove", "sizeof", "typeof", "nameof", "default",
    "checked", "unchecked", "stackalloc", "await", "throw", "when", "is", "as",
))

_TYPE_DECL = re.compile(
    r"\b(?:class|struct|interface|record|enum)\s+(?P<tname>[A-Za-z_][A-Za-z0-9_]*)"
)

# Parameter names differ between overloads that are otherwise identical, and C#
# resolves overloads on TYPES only - so the key must drop names, modifiers and
# default values or two genuinely-conflicting methods will look distinct.
_PARAM_NOISE = re.compile(r"\b(?:in|out|ref|params|this)\b")



NEWLINE = chr(10)
BACKSLASH = chr(92)


def _blank_noncode(text: str) -> str:
    """Replace comments and string literals with spaces, keeping newlines and length.

    Newlines are preserved so reported line numbers stay true, and every replacement is
    one-for-one in length so byte offsets keep mapping to the right line.

    Written with an explicit character scan rather than a regex because C# nests these
    forms: a `//` inside a string is not a comment, a quote inside a verbatim string is
    escaped by doubling, and a regex that tries to hold all of that is how the first
    version of this checker ended up reading test-embedded source as real declarations.
    """
    out = []
    i = 0
    n = len(text)
    while i < n:
        ch = text[i]
        nxt = text[i + 1] if i + 1 < n else ""
        if ch == "/" and nxt == "/":
            while i < n and text[i] != NEWLINE:
                out.append(" ")
                i += 1
            continue
        if ch == "/" and nxt == "*":
            out.append("  ")
            i += 2
            while i < n and not (text[i] == "*" and i + 1 < n and text[i + 1] == "/"):
                out.append(NEWLINE if text[i] == NEWLINE else " ")
                i += 1
            out.append("  ")
            i = min(i + 2, n)
            continue
        if ch == "@" and nxt == '"':
            # Verbatim string: no escapes, and a literal quote is written as "".
            out.append("  ")
            i += 2
            while i < n:
                if text[i] == '"':
                    if i + 1 < n and text[i + 1] == '"':
                        out.append("  ")
                        i += 2
                        continue
                    out.append(" ")
                    i += 1
                    break
                out.append(NEWLINE if text[i] == NEWLINE else " ")
                i += 1
            continue
        if ch == '"':
            out.append(" ")
            i += 1
            while i < n:
                if text[i] == BACKSLASH:
                    out.append("  ")
                    i += 2
                    continue
                if text[i] == '"':
                    out.append(" ")
                    i += 1
                    break
                out.append(NEWLINE if text[i] == NEWLINE else " ")
                i += 1
            continue
        if ch == "'":
            out.append(" ")
            i += 1
            while i < n:
                if text[i] == BACKSLASH:
                    out.append("  ")
                    i += 2
                    continue
                if text[i] == "'":
                    out.append(" ")
                    i += 1
                    break
                out.append(" ")
                i += 1
            continue
        out.append(ch)
        i += 1
    return "".join(out)

def _normalise_params(raw: str) -> str:
    """Reduce a parameter list to a comparable type-only signature."""
    if not raw.strip():
        return ""
    flat = re.sub(r"\s+", " ", raw)
    flat = _PARAM_NOISE.sub("", flat)
    out = []
    depth = 0
    current = []
    for ch in flat:
        if ch in "<([":
            depth += 1
        elif ch in ">)]":
            depth -= 1
        if ch == "," and depth == 0:
            out.append("".join(current))
            current = []
            continue
        current.append(ch)
    if current:
        out.append("".join(current))

    types = []
    for param in out:
        piece = param.split("=")[0].strip()          # drop default values
        piece = re.sub(r"\[[^\]]*\]\s*", "", piece)  # drop attributes
        if not piece:
            continue
        # Split on the LAST top-level space so a generic argument list is not cut in
        # half: "ReadOnlySpan<char> destination" previously normalised to
        # "chardestination", which made two unrelated overloads compare equal.
        depth_scan = 0
        cut = -1
        for idx, c in enumerate(piece):
            if c in "<([":
                depth_scan += 1
            elif c in ">)]":
                depth_scan -= 1
            elif c == " " and depth_scan == 0:
                cut = idx
        type_part = piece[:cut] if cut > 0 else piece
        types.append(re.sub(r"\s+", "", type_part))
    return ",".join(types)


def scan_file(path: str) -> list:
    """Return [(name, signature, [line numbers])] for every duplicated signature."""
    try:
        with open(path, "r", encoding="utf-8-sig", errors="replace") as handle:
            text = handle.read()
    except OSError as error:
        return [("<unreadable>", str(error), [])]

    # Blank out comments and string literals FIRST, preserving newlines so reported
    # line numbers stay true. Without this the scan reads C# source embedded in test
    # string literals as real declarations: a static-analysis test in this project
    # quotes whole methods, and the first version of this checker reported 791
    # duplicates across 3775 files, including signatures like `HandleLanguageChanged(")`
    # whose entire parameter list was a stray quote character. A checker nobody can
    # trust gets switched off, which is worse than not having one.
    text = _blank_noncode(text)

    # Line index for offset -> line number, so a report points at real lines.
    starts = [0]
    for index, ch in enumerate(text):
        if ch == "\n":
            starts.append(index + 1)

    def line_of(offset: int) -> int:
        low, high = 0, len(starts) - 1
        while low < high:
            mid = (low + high + 1) // 2
            if starts[mid] <= offset:
                low = mid
            else:
                high = mid - 1
        return low + 1

    # Attribute the declaration to its enclosing type, because two types in one
    # file may legitimately share a method name.
    type_spans = [(m.start(), m.group("tname")) for m in _TYPE_DECL.finditer(text)]

    def type_at(offset: int) -> str:
        owner = "<file>"
        for start, name in type_spans:
            if start <= offset:
                owner = name
            else:
                break
        return owner

    seen = {}
    for match in _DECL.finditer(text):
        name = match.group("name")
        if name in _NOT_A_METHOD:
            continue
        key = (type_at(match.start()), name,
               (match.group("generics") or "").strip(),
               _normalise_params(match.group("params")))
        seen.setdefault(key, []).append(line_of(match.start()))

    return [(key[1], key[3], lines) for key, lines in seen.items() if len(lines) > 1]


def main(argv: list) -> int:
    targets = argv[1:] or ["Assets/_Project"]
    files = []
    for target in targets:
        if os.path.isfile(target):
            files.append(target)
            continue
        for root, _dirs, names in os.walk(target):
            # Worktrees hold stale full copies of the repo; scanning them reports
            # duplicates that do not exist in the live tree.
            if ".claude" in root or "worktrees" in root:
                continue
            files.extend(os.path.join(root, n) for n in names if n.endswith(".cs"))

    total = 0
    for path in sorted(files):
        for name, signature, lines in scan_file(path):
            total += 1
            print("DUPLICATE  {p}\n           {n}({s})  at lines {l}".format(
                p=path, n=name, s=signature, l=", ".join(str(x) for x in lines)))

    print("scanned {f} files, {t} duplicate signature(s)".format(f=len(files), t=total))
    return 1 if total else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
