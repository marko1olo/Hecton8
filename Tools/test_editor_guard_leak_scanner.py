"""Regression tests for Tools/EditorGuardLeakScanner.py.

Each case is drawn from a defect actually found in the runtime tree, or from a false
positive the scanner produced before it was tightened. The false-positive cases matter as
much as the detections: the first version of this scanner reported 101 hits, of which 67
were noise from name collisions across types, guards that still hold in a player build, and
`#if/#else` pairs supplying alternate definitions.
"""

import EditorGuardLeakScanner as scanner


def _scan(source):
    return scanner.scan_text(source.splitlines(keepends=True), "<test>")


def test_detects_guarded_declaration_called_from_unguarded_code():
    """The SubmarineDynamicsRuntime shape: boot chain fenced, callers not."""
    hits = _scan(
        """
public class Runtime
{
    private void Boot()
    {
        BuildDefaultConfig();
    }
#if UNITY_EDITOR
    private void BuildDefaultConfig()
    {
    }
#endif
}
"""
    )
    assert len(hits) == 1
    assert hits[0]["member"] == "BuildDefaultConfig"
    assert hits[0]["called"] < hits[0]["declared"]


def test_ignores_if_else_alternate_definitions():
    """PlayerTool.PublishLifecycleDebug: an #else supplies the release definition."""
    assert _scan(
        """
public class Tool
{
    private void Spawn()
    {
        PublishLifecycleDebug();
    }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void PublishLifecycleDebug()
    {
    }
#else
    private void PublishLifecycleDebug()
    {
    }
#endif
}
"""
    ) == []


def test_ignores_guards_that_hold_in_a_player_build():
    """UNITY_ADDRESSABLES_EXIST and platform guards are present in player builds."""
    for condition in (
        "UNITY_ADDRESSABLES_EXIST",
        "UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN",
        "!UNITY_WEBGL || UNITY_EDITOR",
    ):
        assert _scan(
            """
public class Widget
{
    private void Boot()
    {
        Prepare();
    }
#if %s
    private void Prepare()
    {
    }
#endif
}
"""
            % condition
        ) == [], condition


def test_ignores_same_member_name_in_a_different_type():
    """Execute/OnEnable/TryEnqueue collide constantly across types in one file."""
    assert _scan(
        """
public class Alpha
{
    private void Run()
    {
        Execute();
    }
    private void Execute()
    {
    }
}
public struct Beta
{
#if UNITY_EDITOR
    private void Execute()
    {
    }
#endif
}
"""
    ) == []


def test_ignores_unguarded_declaration_even_when_a_guarded_one_exists():
    """A guarded overload is harmless while an unguarded declaration also exists."""
    assert _scan(
        """
public class Widget
{
    private void Boot()
    {
        Prepare();
    }
    private void Prepare()
    {
    }
#if UNITY_EDITOR
    private void Prepare(int extra)
    {
    }
#endif
}
"""
    ) == []


def test_detects_call_sited_after_the_guard_closes():
    """A single guarded member whose only caller sits below the #endif."""
    hits = _scan(
        """
public class Survival
{
#if UNITY_EDITOR
    private bool TryReadInjectedRow(string id)
    {
        return false;
    }
#endif
    private bool Refresh()
    {
        return TryReadInjectedRow("id");
    }
}
"""
    )
    assert len(hits) == 1
    assert hits[0]["member"] == "TryReadInjectedRow"
    assert hits[0]["called"] > hits[0]["declared"]


def test_known_blind_spot_overload_split_by_guard():
    """Pins the documented blind spot - see the scanner module docstring.

    The real HectonSurvivalSystem defect had this exact shape: the string overload was
    guarded, the ItemData overload was not, and the latter called the former. Grouping by
    (type, name) means the unguarded overload makes the name look present, so the scanner
    stays silent even though overload resolution fails in a player build. If the scanner
    ever learns signatures this assertion must flip to a detection.
    """
    assert (
        _scan(
            """
public class Survival
{
#if UNITY_EDITOR
    private bool TryGetInjected(string id)
    {
        return false;
    }
#endif
    private bool TryGetInjected(Item item)
    {
        return TryGetInjected(item.Id);
    }
}
"""
        )
        == []
    )


def test_unbalanced_preprocessor_yields_no_false_claims():
    """A file the model cannot classify must stay silent rather than guess."""
    assert _scan(
        """
public class Widget
{
#if UNITY_EDITOR
    private void Prepare()
    {
    }
}
"""
    ) == []


def test_condition_classifier():
    assert scanner.compiled_out_of_player("UNITY_EDITOR")
    assert scanner.compiled_out_of_player("UNITY_EDITOR || DEVELOPMENT_BUILD")
    assert not scanner.compiled_out_of_player("UNITY_ADDRESSABLES_EXIST")
    assert not scanner.compiled_out_of_player("UNITY_EDITOR || UNITY_STANDALONE_WIN")
    assert not scanner.compiled_out_of_player(None)
    assert not scanner.compiled_out_of_player(scanner.ELSE_MARK + "UNITY_EDITOR")


def test_self_test_fixtures_pass():
    assert scanner.self_test() == 0


if __name__ == "__main__":
    import sys

    failed = 0
    for name, fn in sorted(globals().items()):
        if name.startswith("test_") and callable(fn):
            try:
                fn()
                print(f"  [PASS] {name}")
            except AssertionError as error:
                failed += 1
                print(f"  [FAIL] {name}: {error}")
    print(f"{'FAILED' if failed else 'OK'}: {failed} failure(s)")
    sys.exit(1 if failed else 0)
