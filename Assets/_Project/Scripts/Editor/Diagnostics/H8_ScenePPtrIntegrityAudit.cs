using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hecton8.EditorTools.Diagnostics
{
    /// <summary>
    /// READ-ONLY audit of broken PPtrs in scene assets, and of the structural damage a broken PPtr
    /// leaves behind after Unity has already loaded the scene.
    ///
    /// WHY THIS EXISTS. One editor run emits 95 instances of
    ///   "Broken text PPtr in file(Assets/_Project/Scenes/01_MAIN_MENU.unity).
    ///    Local file identifier (N) doesn't exist!"
    /// The message names the dead id and nothing else: not the field, not the owning object, not how
    /// many objects share one dead id. Without that, 95 warnings look like 95 problems instead of the
    /// small number of deleted parents they actually are, and the menu still runs, so the debt is
    /// invisible and survives.
    ///
    /// THE TWO PASSES MEASURE DIFFERENT THINGS AND NEITHER SUBSTITUTES FOR THE OTHER.
    ///
    /// PASS 1 - TEXT PPTR AUDIT is the only pass that can name a dead local file identifier. It reads
    /// the .unity file as text, collects every document anchor (--- !u!CLASS &ID), then collects every
    /// LOCAL reference - a {fileID: N} brace group with no guid inside it, because a group carrying a
    /// guid resolves through the asset database to another file and is not local - and reports each
    /// reference whose target anchor is absent from the file. That reproduces the editor's own count
    /// exactly and adds the owning GameObject and the serialized field name the editor omits.
    ///
    /// PASS 2 - LOADED-SCENE ORPHAN AUDIT measures the consequence. A broken m_Father resolves to
    /// null, so the child becomes a scene ROOT. A UI Graphic with no Canvas above it is not drawn and
    /// receives no raycast, so a wired Button in that state still holds its onClick and can never be
    /// pressed. This pass finds Canvas-less Graphic subtrees among the roots and prints the persistent
    /// onClick targets it finds inside them, which is the player-visible cost.
    ///
    /// PASS 3 - SERIALIZED REFERENCE WALK opens every component with SerializedObject and reports any
    /// object-reference field that is null while still holding a non-zero instance id, plus every
    /// missing script. READ THE CAVEAT: this pass CANNOT see the broken PPtrs from pass 1. By the time
    /// a SerializedObject exists, Unity has already resolved the unresolvable id to plain null and
    /// dropped the instance id, so a broken m_Father is indistinguishable from a legitimate scene
    /// root. If pass 1 reports a large number and pass 3 reports zero, that is the expected result and
    /// the reason pass 1 exists. Pass 3 catches a different family: missing MonoScript assets and
    /// references to deleted assets.
    ///
    /// A TEXT SCENE IS THE ONLY AUDITABLE ONE, AND THAT IS EXPIRING. ProjectSettings/
    /// EditorSettings.asset has m_SerializationMode: 2 (ForceBinary). 01_MAIN_MENU.unity, 00_BOOTSTRAP
    /// .unity and 01_ORBIT.unity are still text on disk; 02_HECTON_WORLD.unity is already binary. The
    /// first time anyone opens and saves a text scene in this project the editor rewrites it as
    /// binary, the orphans are baked in as ordinary scene roots, and every dead id in pass 1 becomes
    /// permanently unreadable. Run pass 1 and keep its output before touching one of these scenes.
    ///
    /// WHY IT NEVER WRITES. AGENTS.md forbids automated passes from calling
    /// EditorSceneManager.SaveScene / MarkSceneDirty / EditorUtility.SetDirty on production assets,
    /// and mutating a scene as text is forbidden outright. This tool calls none of those, opens files
    /// read-only, and touches no SetActive or .enabled. Because opening a scene Single would silently
    /// discard unsaved in-memory work, it REFUSES to run while any loaded scene is dirty.
    ///
    /// USAGE
    ///   Unity.exe -batchmode -quit -projectPath . -logFile Logs/pptr_integrity.log \
    ///     -executeMethod Hecton8.EditorTools.Diagnostics.H8_ScenePPtrIntegrityAudit.Run \
    ///     [-h8PPtrScenes a.unity,b.unity] [-h8PPtrTextOnly 1]
    ///   or the menu item Hecton8/Diagnostics/Scene PPtr Integrity Audit.
    ///
    /// -h8PPtrTextOnly skips passes 2 and 3. Use it when the scene list includes a large scene:
    /// 02_HECTON_WORLD.unity is 6 MB binary and the SerializedObject walk over it is slow, while pass
    /// 1 cannot read it at all.
    /// </summary>
    public static class H8_ScenePPtrIntegrityAudit
    {
        private const string Marker = "[H8_PPTR]";
        private const string MenuPath = "Hecton8/Diagnostics/Scene PPtr Integrity Audit";

        /// <summary>
        /// The text scenes. 02_HECTON_WORLD.unity is deliberately absent: it is already binary, so
        /// pass 1 would report nothing there and that silence must not be mistaken for a clean file.
        /// Pass it explicitly with -h8PPtrScenes if the loaded-scene passes are wanted for it.
        /// </summary>
        private static readonly string[] DefaultScenes =
        {
            "Assets/_Project/Scenes/00_BOOTSTRAP.unity",
            "Assets/_Project/Scenes/01_MAIN_MENU.unity",
            "Assets/_Project/Scenes/01_ORBIT.unity",
        };

        /// <summary>
        /// Root object names that live source requires to exist by name. Absence here is a hard
        /// finding, not a style note, because the code that looks them up resolves by name and
        /// silently does nothing when the lookup fails.
        /// </summary>
        private static readonly ExpectedRoot[] ExpectedRoots =
        {
            new ExpectedRoot(
                "Assets/_Project/Scenes/01_MAIN_MENU.unity",
                "H8_MENU_READABLE_OVERLAY_1428",
                "Assets/_Project/Scripts/Editor/MainMenuValidator.cs:24 requires this Canvas by name and " +
                "MainMenuValidator.cs:28 expects 2 serialized WorldSpace canvases"),
            new ExpectedRoot(
                "Assets/_Project/Scenes/01_MAIN_MENU.unity",
                "H8_MENU_VISUAL_STAGE_1428",
                "Assets/_Project/Scripts/UI/MainMenuAtmosphereController.cs:19 resolves this root by name at " +
                "MainMenuAtmosphereController.cs:148; when it is absent every authored atmosphere quad " +
                "resolves null and EnsureBackdropCold/EnsureHazeCold return without doing anything " +
                "(MainMenuAtmosphereController.cs:296-324)"),
        };

        /// <summary>
        /// Editor-reported baseline for 01_MAIN_MENU.unity, 2026-07-28. Printed next to the measured
        /// number so a change in the scene is visible rather than assumed. This is a comparison
        /// value, never an assertion: the measured number is the evidence.
        /// </summary>
        private const string BaselineScene = "Assets/_Project/Scenes/01_MAIN_MENU.unity";
        private const int BaselineBrokenPPtrOccurrences = 95;

        private const int MaxReferrersPrintedPerDeadId = 40;
        private const int MaxMissingReferencesPrinted = 200;

        private static readonly Regex AnchorPattern =
            new Regex(@"^--- !u!(-?\d+) &(-?\d+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // A LOCAL reference: the brace group closes straight after the number, so it carries no guid
        // and no type. {fileID: 11500000, guid: ..., type: 3} deliberately does not match - that one
        // resolves through the asset database and is not this tool's business.
        private static readonly Regex LocalReferencePattern =
            new Regex(@"\{fileID:\s*(-?\d+)\s*\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // A block header with no inline value, e.g. "  m_Children:". Names the container for the
        // "- {fileID: N}" list entries that follow it.
        private static readonly Regex BlockHeaderPattern =
            new Regex(@"^(\s*)([A-Za-z_][A-Za-z0-9_]*):\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex InlineFieldPattern =
            new Regex(@"^\s*-?\s*([A-Za-z_][A-Za-z0-9_]*):", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly struct ExpectedRoot
        {
            public readonly string ScenePath;
            public readonly string RootName;
            public readonly string Requirement;

            public ExpectedRoot(string scenePath, string rootName, string requirement)
            {
                ScenePath = scenePath;
                RootName = rootName;
                Requirement = requirement;
            }
        }

        private sealed class Referrer
        {
            public int Line;
            public string Field;
            public long OwnerAnchor;
            public int OwnerClassId;
            public string OwnerObjectName;
        }

        private sealed class SceneDocument
        {
            public int ClassId;
            public long Anchor;
            public int StartLine;
            public string Name;
            public long GameObjectAnchor;
        }

        private sealed class TextAuditResult
        {
            public bool Parsed;
            public bool IsText;
            public int AnchorCount;
            public int LineCount;
            public int DanglingOccurrences;
            public readonly Dictionary<long, List<Referrer>> ByDeadId = new Dictionary<long, List<Referrer>>();
        }

        [MenuItem(MenuPath)]
        public static void Run()
        {
            string[] scenes = SplitArg("-h8PPtrScenes", DefaultScenes);
            bool textOnly = ReadArg("-h8PPtrTextOnly") != null;

            if (!SelfTestPassed())
                return;

            if (!textOnly && !DirtySceneGuardPassed())
                return;

            Debug.Log(
                Marker + " START scenes=" + scenes.Length + " textOnly=" + (textOnly ? "1" : "0") +
                " - nothing is written, marked dirty or saved.");

            for (int i = 0; i < scenes.Length; i++)
            {
                string scenePath = scenes[i];
                if (!System.IO.File.Exists(scenePath))
                {
                    Debug.LogWarning(Marker + " MISSING SCENE " + scenePath + " - not audited.");
                    continue;
                }

                Debug.Log(Marker + " ===== SCENE " + scenePath + " =====");
                TextAuditResult text = AuditTextPPtrs(scenePath);
                ReportTextAudit(scenePath, text);

                if (textOnly)
                    continue;

                AuditLoadedScene(scenePath, text);
            }

            Debug.Log(Marker + " DONE - nothing was modified, marked dirty or saved.");
        }

        // ------------------------------------------------------------------------------------------
        // PASS 1 - text PPtr audit
        // ------------------------------------------------------------------------------------------

        private static TextAuditResult AuditTextPPtrs(string scenePath)
        {
            var result = new TextAuditResult();

            string[] lines;
            try
            {
                lines = System.IO.File.ReadAllLines(scenePath);
            }
            catch (Exception ex)
            {
                Debug.LogError(Marker + " FAILED to read " + scenePath + ": " + ex.GetType().Name + ": " + ex.Message);
                return result;
            }

            result.LineCount = lines.Length;
            result.IsText = lines.Length > 0 && lines[0].StartsWith("%YAML", StringComparison.Ordinal);
            if (!result.IsText)
                return result;

            ParseText(lines, result);
            result.Parsed = true;
            return result;
        }

        /// <summary>
        /// Shared by the audit and the self-test so the instrument and its proof cannot diverge.
        /// </summary>
        private static void ParseText(string[] lines, TextAuditResult result)
        {
            var anchors = new HashSet<long>();
            var documents = new List<SceneDocument>();
            var byAnchor = new Dictionary<long, SceneDocument>();

            SceneDocument current = null;
            for (int i = 0; i < lines.Length; i++)
            {
                Match anchor = AnchorPattern.Match(lines[i]);
                if (anchor.Success)
                {
                    long id = ParseLong(anchor.Groups[2].Value);
                    current = new SceneDocument
                    {
                        ClassId = (int)ParseLong(anchor.Groups[1].Value),
                        Anchor = id,
                        StartLine = i + 1,
                    };
                    documents.Add(current);
                    anchors.Add(id);
                    byAnchor[id] = current;
                    continue;
                }

                if (current == null)
                    continue;

                if (current.Name == null && lines[i].StartsWith("  m_Name:", StringComparison.Ordinal))
                    current.Name = lines[i].Substring("  m_Name:".Length).Trim();

                if (current.GameObjectAnchor == 0 && lines[i].StartsWith("  m_GameObject:", StringComparison.Ordinal))
                {
                    Match owner = LocalReferencePattern.Match(lines[i]);
                    if (owner.Success)
                        current.GameObjectAnchor = ParseLong(owner.Groups[1].Value);
                }
            }

            result.AnchorCount = anchors.Count;

            // Second sweep: attribute every dangling local reference to its document, its owning
            // GameObject and the serialized field it sits in.
            int documentCursor = -1;
            string containerField = null;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (AnchorPattern.IsMatch(line))
                {
                    documentCursor++;
                    containerField = null;
                    continue;
                }

                Match header = BlockHeaderPattern.Match(line);
                if (header.Success)
                {
                    containerField = header.Groups[2].Value;
                    continue;
                }

                if (line.IndexOf("fileID:", StringComparison.Ordinal) < 0)
                    continue;

                foreach (Match reference in LocalReferencePattern.Matches(line))
                {
                    long target = ParseLong(reference.Groups[1].Value);
                    if (target == 0 || anchors.Contains(target))
                        continue;

                    SceneDocument owner = documentCursor >= 0 && documentCursor < documents.Count
                        ? documents[documentCursor]
                        : null;

                    string ownerName = "<unknown>";
                    if (owner != null)
                    {
                        if (owner.ClassId == 1)
                        {
                            ownerName = owner.Name ?? "<unnamed>";
                        }
                        else if (owner.GameObjectAnchor != 0 &&
                                 byAnchor.TryGetValue(owner.GameObjectAnchor, out SceneDocument ownerGo))
                        {
                            ownerName = ownerGo.Name ?? "<unnamed>";
                        }
                    }

                    if (!result.ByDeadId.TryGetValue(target, out List<Referrer> referrers))
                    {
                        referrers = new List<Referrer>();
                        result.ByDeadId[target] = referrers;
                    }

                    referrers.Add(new Referrer
                    {
                        Line = i + 1,
                        Field = ResolveFieldName(line, containerField),
                        OwnerAnchor = owner != null ? owner.Anchor : 0L,
                        OwnerClassId = owner != null ? owner.ClassId : 0,
                        OwnerObjectName = ownerName,
                    });
                    result.DanglingOccurrences++;
                }
            }
        }

        private static string ResolveFieldName(string line, string containerField)
        {
            Match inline = InlineFieldPattern.Match(line);
            if (inline.Success)
                return inline.Groups[1].Value;

            return containerField == null ? "<unknown field>" : containerField + "[]";
        }

        private static void ReportTextAudit(string scenePath, TextAuditResult result)
        {
            if (!result.IsText)
            {
                Debug.LogWarning(
                    Marker + "   PASS 1 SKIPPED - " + scenePath + " is BINARY on disk (" + result.LineCount +
                    " text lines read, no %YAML header). A binary scene cannot be text-audited and a text " +
                    "grep over it returns a FALSE ABSENCE. This silence is not a clean bill of health.");
                return;
            }

            if (!result.Parsed)
                return;

            Debug.Log(
                Marker + "   PASS 1 TEXT PPTR AUDIT anchors=" + result.AnchorCount +
                " brokenLocalPPtrOccurrences=" + result.DanglingOccurrences +
                " distinctDeadIds=" + result.ByDeadId.Count);

            if (string.Equals(scenePath, BaselineScene, StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log(
                    Marker + "     baseline: the editor reported " + BaselineBrokenPPtrOccurrences +
                    " 'Broken text PPtr' instances for this scene on 2026-07-28. Measured now: " +
                    result.DanglingOccurrences + ". A difference means the scene changed since that run.");
            }

            if (result.ByDeadId.Count == 0)
            {
                Debug.Log(Marker + "     no broken local PPtrs - every {fileID: N} without a guid resolves to an anchor in this file.");
                return;
            }

            var deadIds = new List<KeyValuePair<long, List<Referrer>>>(result.ByDeadId);
            deadIds.Sort((a, b) => b.Value.Count.CompareTo(a.Value.Count));

            for (int i = 0; i < deadIds.Count; i++)
            {
                long deadId = deadIds[i].Key;
                List<Referrer> referrers = deadIds[i].Value;

                var fields = new Dictionary<string, int>(StringComparer.Ordinal);
                var classes = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int r = 0; r < referrers.Count; r++)
                {
                    fields.TryGetValue(referrers[r].Field, out int f);
                    fields[referrers[r].Field] = f + 1;
                    string className = DescribeClassId(referrers[r].OwnerClassId);
                    classes.TryGetValue(className, out int c);
                    classes[className] = c + 1;
                }

                Debug.LogWarning(
                    Marker + "     DEAD ID " + deadId + " referenced " + referrers.Count +
                    " time(s)  fields=" + Summarize(fields, 6) + "  referringDocuments=" + Summarize(classes, 6));

                int printed = Math.Min(referrers.Count, MaxReferrersPrintedPerDeadId);
                for (int r = 0; r < printed; r++)
                {
                    Referrer referrer = referrers[r];
                    Debug.Log(
                        Marker + "       held by '" + referrer.OwnerObjectName + "' (" +
                        DescribeClassId(referrer.OwnerClassId) + " anchor " + referrer.OwnerAnchor +
                        ") field " + referrer.Field + " at " + scenePath + ":" + referrer.Line);
                }

                if (referrers.Count > printed)
                    Debug.Log(Marker + "       ...+" + (referrers.Count - printed) + " more referrer(s) not printed.");
            }

            Debug.LogError(
                Marker + "     VERDICT " + result.DanglingOccurrences + " broken local PPtr(s) from only " +
                result.ByDeadId.Count + " distinct dead id(s). The fan-out is the finding: each dead id is one " +
                "absent document, not one broken object. Where the field is m_Father the absent document was a " +
                "PARENT and every referrer became a scene root at load - see pass 2 for what that costs. " +
                "Repair is an authoring decision and this tool makes none.");
        }

        // ------------------------------------------------------------------------------------------
        // PASS 2 - loaded-scene orphan audit, and PASS 3 - serialized reference walk
        // ------------------------------------------------------------------------------------------

        private static void AuditLoadedScene(string scenePath, TextAuditResult text)
        {
            Scene scene;
            try
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }
            catch (Exception ex)
            {
                Debug.LogError(Marker + " FAILED to open " + scenePath + ": " + ex.GetType().Name + ": " + ex.Message);
                return;
            }

            if (!scene.IsValid())
            {
                Debug.LogError(Marker + " INVALID SCENE " + scenePath);
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            Debug.Log(Marker + "   PASS 2 LOADED-SCENE ORPHAN AUDIT roots=" + roots.Length);

            var canvasLessRoots = new List<GameObject>();
            int canvasCount = 0;
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root.GetComponent<Canvas>() != null)
                {
                    canvasCount++;
                    continue;
                }

                // A root has no ancestors, so "no Canvas on this object" means "no Canvas anywhere
                // above these Graphics". Unity draws a Graphic only through a Canvas and raycasts
                // only through a GraphicRaycaster on one, so this whole subtree is inert.
                if (CountCanvasRendererGraphics(root) > 0)
                    canvasLessRoots.Add(root);
            }

            Debug.Log(Marker + "     roots carrying a Canvas: " + canvasCount);
            ConfirmBrokenFatherPromotion(text, roots);
            ReportExpectedRoots(scenePath, roots);

            if (canvasLessRoots.Count == 0)
            {
                Debug.Log(Marker + "     no Canvas-less UI roots - every Graphic in this scene has a Canvas above it.");
            }
            else
            {
                int inertObjects = 0;
                int inertGraphics = 0;
                int inertSelectables = 0;
                for (int i = 0; i < canvasLessRoots.Count; i++)
                {
                    GameObject root = canvasLessRoots[i];
                    Transform[] all = root.GetComponentsInChildren<Transform>(true);
                    int graphics = CountCanvasRendererGraphics(root);
                    Selectable[] selectables = root.GetComponentsInChildren<Selectable>(true);
                    inertObjects += all.Length;
                    inertGraphics += graphics;
                    inertSelectables += selectables.Length;

                    Debug.LogWarning(
                        Marker + "     UI ROOT WITH NO CANVAS '" + root.name + "'  objects=" + all.Length +
                        "  canvasGraphics=" + graphics + "  selectables=" + selectables.Length +
                        "  activeSelf=" + (root.activeSelf ? "1" : "0") +
                        " - not drawn and not raycastable in this state.",
                        root);

                    ReportPersistentCalls(root, selectables);
                }

                Debug.LogError(
                    Marker + "     VERDICT " + canvasLessRoots.Count + " root(s) hold " + inertObjects +
                    " object(s), " + inertGraphics + " Graphic(s) and " + inertSelectables +
                    " Selectable(s) that cannot draw or be clicked because no Canvas is above them. " +
                    "A root that is a UI element is the signature of a broken m_Father: the parent is " +
                    "gone, so the child was promoted to a root at load. Cross-check the count against " +
                    "pass 1.");
            }

            AuditSerializedReferences(roots);
        }

        /// <summary>
        /// Graphics that actually need a Canvas: the ones drawing through a CanvasRenderer. Image,
        /// legacy Text and TextMeshProUGUI qualify. 3D TextMeshPro derives from Graphic too but draws
        /// through a MeshRenderer and needs no Canvas, so counting bare Graphic would over-report.
        /// </summary>
        private static int CountCanvasRendererGraphics(GameObject root)
        {
            Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
            int count = 0;
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null && graphics[i].GetComponent<CanvasRenderer>() != null)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Closes the inference gap between the two passes. Pass 1 proves a m_Father PPtr is dead; the
        /// claim that the child therefore becomes a scene ROOT is engine behaviour, not a measurement.
        /// This checks it: every object pass 1 found holding a dead m_Father is looked for among the
        /// loaded scene's roots. Matching is BY NAME and pass 1's orphans are not uniquely named
        /// ("Background", "Label", "Scanline_0" repeat), so this confirms the mechanism and sizes it -
        /// it does not identify individual objects. Pass 1 owns identity.
        ///
        /// A name in pass 1 that is NOT a root would mean the promotion model is wrong, so it is
        /// reported as an error against this tool rather than against the scene.
        /// </summary>
        private static void ConfirmBrokenFatherPromotion(TextAuditResult text, GameObject[] roots)
        {
            if (!text.Parsed)
                return;

            var orphanNames = new HashSet<string>(StringComparer.Ordinal);
            int fatherReferrers = 0;
            foreach (KeyValuePair<long, List<Referrer>> entry in text.ByDeadId)
            {
                for (int i = 0; i < entry.Value.Count; i++)
                {
                    if (!string.Equals(entry.Value[i].Field, "m_Father", StringComparison.Ordinal))
                        continue;

                    fatherReferrers++;
                    orphanNames.Add(entry.Value[i].OwnerObjectName);
                }
            }

            if (fatherReferrers == 0)
                return;

            var rootNames = new HashSet<string>(StringComparer.Ordinal);
            int matchedRoots = 0;
            for (int i = 0; i < roots.Length; i++)
            {
                rootNames.Add(roots[i].name);
                if (orphanNames.Contains(roots[i].name))
                    matchedRoots++;
            }

            var unmatched = new List<string>();
            foreach (string name in orphanNames)
            {
                if (!rootNames.Contains(name))
                    unmatched.Add(name);
            }

            Debug.Log(
                Marker + "     PROMOTION CHECK pass 1 found " + fatherReferrers +
                " object(s) holding a dead m_Father across " + orphanNames.Count +
                " distinct name(s); " + matchedRoots + " scene root(s) now carry one of those names.");

            if (unmatched.Count == 0)
            {
                Debug.Log(
                    Marker + "       CONFIRMED every name that holds a dead m_Father is present among the " +
                    "scene roots, so a broken parent PPtr does promote the child to a root at load.");
                return;
            }

            Debug.LogError(
                Marker + "       MODEL MISMATCH " + unmatched.Count + " name(s) hold a dead m_Father but are " +
                "not scene roots: " + string.Join(", ", unmatched) + ". Either those objects were nested " +
                "under another orphan (expected, if the dead parent had a surviving grandchild chain) or " +
                "this tool's promotion model is wrong. Do not quote the inert-UI verdict until this is " +
                "explained.");
        }

        private static void ReportExpectedRoots(string scenePath, GameObject[] roots)
        {
            for (int i = 0; i < ExpectedRoots.Length; i++)
            {
                ExpectedRoot expected = ExpectedRoots[i];
                if (!string.Equals(expected.ScenePath, scenePath, StringComparison.OrdinalIgnoreCase))
                    continue;

                bool found = false;
                for (int r = 0; r < roots.Length; r++)
                {
                    if (string.Equals(roots[r].name, expected.RootName, StringComparison.Ordinal))
                    {
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    Debug.Log(Marker + "     REQUIRED ROOT PRESENT '" + expected.RootName + "'");
                    continue;
                }

                Debug.LogError(
                    Marker + "     REQUIRED ROOT ABSENT '" + expected.RootName + "' - " + expected.Requirement +
                    ". The lookup is by name and fails silently, so nothing throws and nothing works.");
            }
        }

        /// <summary>
        /// A Button that kept its onClick while losing its Canvas is the most misleading state in the
        /// scene: the wiring inspects as correct and the control is unreachable. Naming the target and
        /// method makes the loss concrete instead of "some UI is broken".
        /// </summary>
        private static void ReportPersistentCalls(GameObject root, Selectable[] selectables)
        {
            for (int i = 0; i < selectables.Length; i++)
            {
                Button button = selectables[i] as Button;
                if (button == null)
                    continue;

                UnityEventBase clicked = button.onClick;
                int calls = clicked.GetPersistentEventCount();
                if (calls == 0)
                {
                    Debug.Log(
                        Marker + "       unreachable Selectable '" + button.name +
                        "' (no persistent onClick entries)", button);
                    continue;
                }

                for (int c = 0; c < calls; c++)
                {
                    UnityEngine.Object target = clicked.GetPersistentTarget(c);
                    string targetName = target == null ? "<null target>" : target.GetType().Name + " on '" + target.name + "'";
                    Debug.LogWarning(
                        Marker + "       UNREACHABLE WIRED BUTTON '" + root.name + "/" + button.name +
                        "' onClick -> " + targetName + "." + clicked.GetPersistentMethodName(c) +
                        " - the wiring is intact and the button can never be pressed.",
                        button);
                }
            }
        }

        private static void AuditSerializedReferences(GameObject[] roots)
        {
            int componentsWalked = 0;
            int propertiesWalked = 0;
            int missingScripts = 0;
            int missingReferences = 0;
            int printed = 0;

            for (int i = 0; i < roots.Length; i++)
            {
                Transform[] all = roots[i].GetComponentsInChildren<Transform>(true);
                for (int t = 0; t < all.Length; t++)
                {
                    Transform node = all[t];
                    string path = BuildPath(node);

                    Component[] components = node.GetComponents<Component>();
                    for (int c = 0; c < components.Length; c++)
                    {
                        Component component = components[c];

                        // GetComponents returns a null entry where the script asset is missing. That
                        // is a real defect with no Type to attribute it to.
                        if (component == null)
                        {
                            missingScripts++;
                            if (printed < MaxMissingReferencesPrinted)
                            {
                                Debug.LogWarning(
                                    Marker + "       MISSING SCRIPT at " + path + " (component slot " + c + ")",
                                    node);
                                printed++;
                            }

                            continue;
                        }

                        componentsWalked++;
                        SerializedObject serialized = new SerializedObject(component);

                        // GetIterator + Next(true) visits hidden properties too; NextVisible would
                        // skip m_Father and every other [HideInInspector] reference.
                        SerializedProperty property = serialized.GetIterator();
                        while (property.Next(true))
                        {
                            propertiesWalked++;
                            if (property.propertyType != SerializedPropertyType.ObjectReference)
                                continue;

                            if (property.objectReferenceValue != null)
                                continue;

                            // Null with a live id is a reference to something that no longer loads.
                            // Null with id 0 is either an intentional empty slot or a PPtr the loader
                            // already flattened to null - pass 1 owns that case.
                            //
                            // Unity 6000.5 marks objectReferenceInstanceIDValue obsolete with
                            // error:true, so it is a hard CS0619 that no pragma can suppress, and it was
                            // failing the ENTIRE Hecton8.Editor assembly - taking down every editor tool
                            // and content generator in the project, not just this audit.
                            // objectReferenceEntityIdValue is the sanctioned replacement. Do NOT route
                            // it through int: EntityId's implicit int operator is ALSO obsolete with
                            // error:true ("EntityId will not be representable by an int in the
                            // future"), so casting to int just trades one CS0619 for another - measured,
                            // that was this fix's first attempt. Compare the struct to its own default
                            // via ValueType.Equals, which needs no operator this file has not verified.
                            var referenceEntityId = property.objectReferenceEntityIdValue;
                            if (referenceEntityId.Equals(default))
                                continue;

                            missingReferences++;
                            if (printed < MaxMissingReferencesPrinted)
                            {
                                Debug.LogWarning(
                                    Marker + "       MISSING REFERENCE " + path + " [" +
                                    component.GetType().Name + "] ." + property.propertyPath +
                                    " entityId=" + referenceEntityId,
                                    component);
                                printed++;
                            }
                        }
                    }
                }
            }

            Debug.Log(
                Marker + "   PASS 3 SERIALIZED REFERENCE WALK components=" + componentsWalked +
                " properties=" + propertiesWalked + " missingScripts=" + missingScripts +
                " missingReferences=" + missingReferences);

            if (printed >= MaxMissingReferencesPrinted)
                Debug.LogWarning(Marker + "     output truncated at " + MaxMissingReferencesPrinted + " entries.");

            if (missingScripts == 0 && missingReferences == 0)
            {
                Debug.Log(
                    Marker + "     no missing scripts and no null-with-instance-id references. This does NOT " +
                    "clear the scene of broken PPtrs: an unresolvable local file identifier is already plain " +
                    "null here with instance id 0 and is invisible to this pass. Pass 1 is the one that sees it.");
            }
        }

        private static string BuildPath(Transform node)
        {
            var builder = new StringBuilder(96);
            builder.Append(node.name);
            for (Transform parent = node.parent; parent != null; parent = parent.parent)
                builder.Insert(0, parent.name + "/");

            return builder.ToString();
        }

        // ------------------------------------------------------------------------------------------
        // Guards, self-test, helpers
        // ------------------------------------------------------------------------------------------

        /// <summary>
        /// Opening a scene with OpenSceneMode.Single throws away unsaved in-memory edits without
        /// asking in batchmode. A diagnostic is never worth destroying uncommitted work, so a dirty
        /// scene stops the run and names itself.
        /// </summary>
        private static bool DirtySceneGuardPassed()
        {
            var dirty = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.isDirty)
                    dirty.Add(scene.path.Length == 0 ? "<untitled>" : scene.path);
            }

            if (dirty.Count == 0)
                return true;

            Debug.LogError(
                Marker + " REFUSED - " + dirty.Count + " loaded scene(s) have unsaved changes: " +
                string.Join(", ", dirty) + ". Passes 2 and 3 open scenes Single, which would discard " +
                "those edits. Save or revert them and run again, or use -h8PPtrTextOnly 1 which never " +
                "opens a scene. No report was produced and nothing was changed.");
            return false;
        }

        /// <summary>
        /// Known-answer cases on an embedded sample, run before anything is printed. The whole
        /// argument of pass 1 is "a brace group with a guid is not a local reference" and "an absent
        /// anchor is a dead id"; both are checked here against answers that cannot drift, along with
        /// the field attribution. A failure suppresses the report, because a PPtr auditor that
        /// miscounts is worse than none.
        /// </summary>
        private static bool SelfTestPassed()
        {
            string[] positive =
            {
                "%YAML 1.1",
                "%TAG !u! tag:unity3d.com,2011:",
                "--- !u!1 &100",
                "GameObject:",
                "  m_Name: SelfTestOwner",
                "--- !u!224 &200",
                "RectTransform:",
                "  m_GameObject: {fileID: 100}",
                "  m_Children:",
                "  - {fileID: 999999}",
                "  m_Father: {fileID: 4242}",
                "  m_Sprite: {fileID: 21300000, guid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, type: 3}",
                "  m_Nothing: {fileID: 0}",
            };

            var probe = new TextAuditResult();
            ParseText(positive, probe);

            if (probe.AnchorCount != 2)
            {
                Debug.LogError(
                    Marker + " SELF-TEST FAILED anchor scan found " + probe.AnchorCount +
                    " anchors in a 2-anchor sample, so every dead-id verdict would be wrong. Report suppressed.");
                return false;
            }

            if (probe.DanglingOccurrences != 2 || probe.ByDeadId.Count != 2)
            {
                Debug.LogError(
                    Marker + " SELF-TEST FAILED expected exactly 2 dangling local refs from 2 distinct ids " +
                    "(999999 and 4242); measured occurrences=" + probe.DanglingOccurrences +
                    " distinct=" + probe.ByDeadId.Count + ". Either the guid-bearing reference or " +
                    "{fileID: 0} was miscounted. Report suppressed.");
                return false;
            }

            if (!probe.ByDeadId.TryGetValue(4242L, out List<Referrer> fatherReferrers) ||
                fatherReferrers.Count != 1 ||
                !string.Equals(fatherReferrers[0].Field, "m_Father", StringComparison.Ordinal) ||
                !string.Equals(fatherReferrers[0].OwnerObjectName, "SelfTestOwner", StringComparison.Ordinal))
            {
                Debug.LogError(
                    Marker + " SELF-TEST FAILED the m_Father case did not attribute to field 'm_Father' on " +
                    "'SelfTestOwner', so owner and field reporting cannot be trusted. Report suppressed.");
                return false;
            }

            if (!probe.ByDeadId.TryGetValue(999999L, out List<Referrer> childReferrers) ||
                childReferrers.Count != 1 ||
                !string.Equals(childReferrers[0].Field, "m_Children[]", StringComparison.Ordinal))
            {
                Debug.LogError(
                    Marker + " SELF-TEST FAILED a '- {fileID: N}' list entry did not attribute to " +
                    "'m_Children[]', so list-held references would be reported without a field. Report suppressed.");
                return false;
            }

            string[] negative =
            {
                "%YAML 1.1",
                "--- !u!1 &100",
                "GameObject:",
                "  m_Name: Clean",
                "--- !u!4 &200",
                "Transform:",
                "  m_GameObject: {fileID: 100}",
                "  m_Father: {fileID: 0}",
            };

            var control = new TextAuditResult();
            ParseText(negative, control);
            if (control.DanglingOccurrences != 0)
            {
                Debug.LogError(
                    Marker + " SELF-TEST FAILED a clean sample reported " + control.DanglingOccurrences +
                    " broken PPtr(s). The auditor produces false positives. Report suppressed.");
                return false;
            }

            Debug.Log(
                Marker + " SELF-TEST PASSED 2 dead ids found in the positive sample with correct owner and " +
                "field attribution, guid-bearing and {fileID: 0} references correctly ignored, clean sample clean.");
            return true;
        }

        private static string DescribeClassId(int classId)
        {
            switch (classId)
            {
                case 0: return "unattributed";
                case 1: return "GameObject";
                case 4: return "Transform";
                case 20: return "Camera";
                case 23: return "MeshRenderer";
                case 33: return "MeshFilter";
                case 65: return "BoxCollider";
                case 114: return "MonoBehaviour";
                case 222: return "CanvasRenderer";
                case 223: return "Canvas";
                case 224: return "RectTransform";
                case 225: return "CanvasGroup";
                case 1001: return "PrefabInstance";
                default: return "classId=" + classId;
            }
        }

        private static long ParseLong(string value)
        {
            return long.TryParse(value, out long parsed) ? parsed : 0L;
        }

        private static string Summarize(Dictionary<string, int> counts, int take)
        {
            var list = new List<KeyValuePair<string, int>>(counts);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));

            var builder = new StringBuilder();
            for (int i = 0; i < list.Count && i < take; i++)
            {
                if (builder.Length > 0)
                    builder.Append(", ");
                builder.Append(list[i].Key).Append('=').Append(list[i].Value);
            }

            if (list.Count > take)
                builder.Append(", ...+").Append(list.Count - take).Append(" more");

            return builder.Length == 0 ? "<none>" : builder.ToString();
        }

        private static string[] SplitArg(string name, string[] fallback)
        {
            string raw = ReadArg(name);
            if (string.IsNullOrEmpty(raw))
                return fallback;

            // StringSplitOptions.TrimEntries is .NET 5 and this compiles against netstandard2.1.
            string[] parts = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
                parts[i] = parts[i].Trim();

            return parts;
        }

        private static string ReadArg(string name)
        {
            // Hecton8.Environment shadows System.Environment inside this namespace root.
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.Ordinal))
                    return args[i + 1];
            }

            return null;
        }
    }
}
