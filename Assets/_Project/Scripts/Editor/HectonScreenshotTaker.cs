#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Menu entry for the underwater route shot.
///
/// WHAT THIS USED TO DO, and why none of it survived:
///
/// * it wrote the PNG to `C:/Users/Admin/.gemini/antigravity/brain/7b5d06d2-.../
///   underwater_capture.png`, a hardcoded absolute developer path banned outright by
///   `AGENTS.md:126` - on any other machine, and on this one after that scratch folder is cleared,
///   the capture went nowhere and the menu item still reported success;
/// * it subscribed `EditorApplication.update += WaitAndCapture` and never unsubscribed. There was no
///   `-=` anywhere in the file. `COMMON_SENSE.md:62` requires every `+=` to have a guaranteed `-=`;
///   the leaked callback kept counting frames for the rest of the editor session and re-fired
///   `EditorApplication.Exit(0)` behaviour off a static counter;
/// * it called `EditorSceneManager.OpenScene` unconditionally, discarding whatever unsaved scene
///   work was open, then forced Play Mode from a menu click;
/// * it resolved the camera through `Camera.main`, which is banned by `AGENTS.md:336` and only ever
///   finds a camera tagged `MainCamera`;
/// * it waited on a raw frame counter (200 frames, then 400), a unit measured in this editor at
///   roughly one game frame per wall second - see
///   `Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessPlayModeProbe.cs:61-65`.
///
/// It is now a thin, honest caller of the one capture owner,
/// `Hecton8.EditorTools.H8_RouteCaptureStation`, which is non-mutating, writes to a project-relative
/// per-run directory, and states its own capture truth. No subscription, no scene load, no forced
/// Play Mode, no `EditorApplication.Exit`.
/// </summary>
public static class HectonScreenshotTaker
{
    /// <summary>
    /// Captures the currently loaded editor state. If you want the underwater route specifically,
    /// open `02_HECTON_WORLD` yourself (or enter Play Mode and swim there) and then use this - a
    /// menu item is not allowed to throw away your unsaved work to stage its own shot.
    ///
    /// Declared shot list per `TASTE.md:403-412`: an underwater route frame is claimed to carry a
    /// pressure cue and a route cue. The station rejects the claim if the frame it is attached to is
    /// blank or contains no visible renderer, and it never upgrades the claim into acceptance -
    /// `Docs/QUALITY_GATES.md:176`.
    /// </summary>
    [MenuItem("Tools/Hecton/Take Underwater Screenshot", priority = 242)]
    public static void TakeScreenshot()
    {
        Hecton8.EditorTools.H8CaptureVerdict verdict =
            Hecton8.EditorTools.H8_RouteCaptureStation.CaptureCurrentEditorState(
                "underwater",
                Hecton8.EditorTools.H8ShotCue.Pressure | Hecton8.EditorTools.H8ShotCue.Route,
                out string runDirectory);

        if (verdict == Hecton8.EditorTools.H8CaptureVerdict.EvidenceEligible)
        {
            Debug.Log($"[HectonScreenshotTaker] Capture written to {runDirectory}. Verdict: {verdict}.");
            return;
        }

        Debug.LogWarning(
            $"[HectonScreenshotTaker] Capture rejected: {verdict}. Details in {runDirectory ?? "<no directory>"}" +
            "/capture_truth.txt.");
    }
}
#endif
