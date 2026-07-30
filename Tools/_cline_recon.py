# scratch recon - do not promote
import re
import os

OUT = r"C:\hades\Hecton8\Tools\_cline_recon_out.txt"


def main():
    lines_out = []

    def p(*a):
        lines_out.append(" ".join(str(x) for x in a))

    files = [
        r"C:\hades\Hecton8\Assets\_Project\Scripts\Editor\PlayModeSmokeTester.cs",
        r"C:\hades\Hecton8\Assets\_Project\Scripts\BuildTools\BuildPlaytestEntry.cs",
        r"C:\hades\Hecton8\Assets\_Project\Scripts\SaveSystemRuntimeSmokeTester.cs",
        r"C:\hades\Hecton8\Assets\_Project\Scripts\Physics\KCC\HectonKccRuntime_SmokeTest.cs",
        r"C:\hades\Hecton8\Assets\_Project\Scripts\FaunaRuntimeSmokeTester.cs",
        r"C:\hades\Hecton8\Assets\_Project\Scripts\ToolRuntimeSmokeTester.cs",
        r"C:\hades\Hecton8\Assets\_Project\Scripts\Editor\AutomationSmokeTestRunner.cs",
        r"C:\hades\Hecton8\Assets\_Project\Scripts\Editor\FaunaRuntimeSmokeTesterRunner.cs",
        r"C:\hades\Hecton8\Assets\_Project\Scripts\Physics\KCC\Editor\Shinobu355KccSmokeEditorFacade.cs",
    ]
    for f in files:
        p("====", os.path.basename(f), "====")
        try:
            text = open(f, encoding="utf-8", errors="replace").read().splitlines()
            p("path", f)
            p("lines", len(text))
            for i, l in enumerate(text[:100], 1):
                p("%d|%s" % (i, l))
            p("--- key matches ---")
            pat = re.compile(
                r"MenuItem|executeMethod|public static void|class |Run\(|PrecisionDrift|600|BOOTSTRAP|SaveRound|Fail|Pass|SceneManager|LoadScene",
                re.I,
            )
            for i, l in enumerate(text, 1):
                if pat.search(l):
                    p("%d|%s" % (i, l[:200]))
        except Exception as e:
            p("ERR", e)

    for wf in [
        r"C:\hades\Hecton8\.github\workflows\pages.yml",
        r"C:\hades\Hecton8\.github\workflows\deploy-gh-pages.yml",
        r"C:\hades\Hecton8\.github\workflows\static.yml",
    ]:
        p("==== WF", os.path.basename(wf), "====")
        try:
            p(open(wf, encoding="utf-8", errors="replace").read())
        except Exception as e:
            p(e)

    t = open(r"C:\hades\Hecton8\README.md", encoding="utf-8", errors="replace").read()
    p("==== README owner refs ====")
    p("barsukdana", t.count("barsukdana"))
    p("marko1olo", t.count("marko1olo"))

    bp = open(
        r"C:\hades\Hecton8\BUILD_PLAYTEST_ISSUES.md", encoding="utf-8", errors="replace"
    ).read().splitlines()
    p("==== playtest key lines ====")
    for i, l in enumerate(bp, 1):
        low = l.lower()
        if any(
            k in low
            for k in (
                "world",
                "graveyard",
                "15-min",
                "15 min",
                "checklist",
                "d7e461",
                "apply not",
                "captain",
                "smoke gate",
                "v0",
            )
        ):
            p("%d|%s" % (i, l[:180]))

    # existing V0 gate?
    p("==== search V0 / merge gate names ====")
    roots = [
        r"C:\hades\Hecton8\Assets\_Project\Scripts",
        r"C:\hades\Hecton8\Tools",
    ]
    for root in roots:
        for dirpath, _, filenames in os.walk(root):
            if any(x in dirpath for x in ("Library", "Temp", "obj", "worktrees")):
                continue
            for name in filenames:
                if not name.endswith((".cs", ".py", ".md", ".ps1", ".sh")):
                    continue
                path = os.path.join(dirpath, name)
                try:
                    body = open(path, encoding="utf-8", errors="replace").read()
                except Exception:
                    continue
                if re.search(r"V0Smoke|MergeGate|VerticalSliceGate|PlaytestGate|H8_V0", body):
                    p("HIT", path)

    open(OUT, "w", encoding="utf-8").write("\n".join(lines_out))
    print("WROTE", OUT, "bytes", os.path.getsize(OUT))


if __name__ == "__main__":
    main()
