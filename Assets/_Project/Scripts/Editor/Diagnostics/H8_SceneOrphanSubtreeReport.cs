using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools.Diagnostics
{
    /// <summary>
    /// READ-ONLY report on what a broken text PPtr actually COST, reconstructed from the scene file as
    /// text only. It never opens a scene, never loads an object, and never writes.
    ///
    /// WHY THIS EXISTS ALONGSIDE H8_ScenePPtrIntegrityAudit. That tool answers "which local file
    /// identifiers are dead and who points at them", and it answers it correctly. It cannot answer the
    /// question the repair decision actually needs, and the gap is structural, not a bug:
    ///
    ///  - Its pass 1 reports DIRECT referrers of a dead id. In 01_MAIN_MENU.unity the dead id
    ///    637541791 has exactly ONE direct referrer, so pass 1 prints one line for it and ranks it
    ///    last. That single referrer is 'ReadableCommandPanel', and it is the root of a 12-object
    ///    subtree containing three Buttons wired to the LIVE MainMenuController - StartGame,
    ///    OpenSaveLoadMenu and StartOrbitPrologue. Referrer count is not damage; subtree size is.
    ///  - Its passes 2 and 3 can see that subtree, but only by opening the scene Single, which that
    ///    tool correctly REFUSES to do while any loaded scene is dirty. So the moment another agent
    ///    has unsaved work - the normal state of this project - the only pass that could name the
    ///    unreachable buttons is unavailable. This tool has no such precondition, because it reads
    ///    bytes.
    ///
    /// WHAT IT ADDS, all of it derivable from text and none of it printed by the sibling:
    ///   1. The full orphaned SUBTREE under each dead parent, not just the direct referrers.
    ///   2. Every persistent UnityEvent call inside those subtrees, with target object and method
    ///      name, and whether the target is still alive. Intact wiring on an undrawable object is the
    ///      most misleading state a scene can hold: it inspects as correct and can never fire.
    ///   3. The authored m_Text of every label in the subtrees. Text is what identifies the deleted
    ///      parent. Three dead parents in 01_MAIN_MENU each own 24 children with identical names plus
    ///      a VariantLabel reading ORBITAL TELEMETRY, DOCK OPS and ABYSS GATE - that is one authored
    ///      template instanced three times, which no count of RectTransforms could have told anyone.
    ///   4. The RectTransform anchoring of each direct orphan. Fractional m_AnchorMin/m_AnchorMax with
    ///      a zero m_SizeDelta sizes an element from its PARENT rect. Promoted to a scene root there is
    ///      no parent rect, so the element's rect collapses to zero and it cannot draw even if a Canvas
    ///      were put back above it. This is why "reparent it" is not the whole repair.
    ///   5. Whether the names live source resolves BY NAME are present. A by-name lookup that misses
    ///      returns null and the caller does nothing - no exception, no log, no visible failure.
    ///
    /// ABSENCE PROOF IS ONLY VALID ON A TEXT SCENE, AND THAT IS EXPIRING. ProjectSettings/
    /// EditorSettings.asset has m_SerializationMode: 2 (ForceBinary). 01_MAIN_MENU.unity is still text;
    /// 02_HECTON_WORLD.unity is already binary. On a binary file every claim below would be a FALSE
    /// ABSENCE, so this tool refuses to make any and says so. The first save of a text scene converts
    /// it, bakes the orphans in as ordinary roots, and makes the dead ids permanently unreadable.
    ///
    /// WHY IT NEVER WRITES. AGENTS.md forbids automated passes from calling
    /// EditorSceneManager.SaveScene / MarkSceneDirty / EditorUtility.SetDirty on production assets, and
    /// mutating a scene as text is forbidden outright. This tool calls none of those and opens no
    /// scene, so unlike the sibling's passes 2 and 3 it needs no dirty-scene guard: there is nothing it
    /// could discard. Repair is an authoring decision and this tool makes none.
    ///
    /// MEASURED ON 01_MAIN_MENU.unity, 2026-07-28, by parsing the file. 1329 documents, 95 orphaned
    /// transforms, 8 absent parents, 106 objects dragged out of the hierarchy, 0 dead references that
    /// are not m_Father. Kept here so a future run can be compared against it rather than trusted:
    ///
    ///   id 340626855   24 direct  24 objects  all CanvasRenderer  VariantLabel "ORBITAL TELEMETRY"
    ///   id 1720630255  24 direct  24 objects  all CanvasRenderer  VariantLabel "DOCK OPS"
    ///   id 1953018389  24 direct  24 objects  all CanvasRenderer  VariantLabel "ABYSS GATE"
    ///   id 1128787734  19 direct  19 objects  all MeshRenderer    Stage_Title "HECTON-8"
    ///   id 637541791    1 direct  12 objects  all CanvasRenderer  3 buttons wired to a LIVE target
    ///   id 907874022    1 direct   1 object   CanvasRenderer      Text "ORBIT"
    ///   id 1829538190   1 direct   1 object   CanvasRenderer      Text "DOCK"
    ///   id 1658479462   1 direct   1 object   CanvasRenderer      Text "ABYSS"
    ///
    /// The three 24-child groups share one child-name set and differ only in their VariantLabel, and
    /// the three single Text orphans read ORBIT, DOCK and ABYSS - the selector labels for those same
    /// three variants. Docs/Archive/Batch015/Tasks/Status_1428.md:116 records "01_MAIN_MENU now uses
    /// H8_MENU_READABLE_OVERLAY_1428 with three selectable visual variants ... Variant switch and
    /// BTN_Readable_Descend were verified in PlayMode". That is the same overlay whose name is now
    /// absent, and the same button that is now unreachable.
    ///
    /// USAGE
    ///   Unity.exe -batchmode -quit -projectPath . -logFile Logs/orphan_subtree.log \
    ///     -executeMethod Hecton8.EditorTools.Diagnostics.H8_SceneOrphanSubtreeReport.Run \
    ///     [-h8OrphanScenes a.unity,b.unity]
    ///   or the menu item Hecton8/Diagnostics/Scene Orphan Subtree Report.
    /// </summary>
    public static class H8_SceneOrphanSubtreeReport
    {
        private const string Marker = "[H8_ORPHAN_TREE]";
        private const string MenuPath = "Hecton8/Diagnostics/Scene Orphan Subtree Report";

        /// <summary>
        /// Text scenes only. 02_HECTON_WORLD.unity is deliberately absent: it is already binary, so
        /// this tool would report zero orphans there and that silence must not be read as clean.
        /// </summary>
        private static readonly string[] DefaultScenes =
        {
            "Assets/_Project/Scenes/00_BOOTSTRAP.unity",
            "Assets/_Project/Scenes/01_MAIN_MENU.unity",
            "Assets/_Project/Scenes/01_ORBIT.unity",
        };

        private const int TransformClassId = 4;
        private const int RectTransformClassId = 224;
        private const int GameObjectClassId = 1;
        private const int MeshRendererClassId = 23;
        private const int CanvasRendererClassId = 222;
        private const int CanvasClassId = 223;
        private const int MonoBehaviourClassId = 114;

        private const int MaxDirectOrphansPrintedPerParent = 30;
        private const int MaxSubtreeNodesPrintedPerOrphan = 40;

        /// <summary>
        /// Measured on 2026-07-28 by parsing the file, and it reproduces the editor's own occurrence
        /// count exactly. The distinct-id number is here because the figure in circulation before this
        /// run was SIX, taken by hand from a console log. Six is wrong: there are EIGHT, and the two
        /// that the hand extraction dropped - 637541791 and 1829538190 - are both single-occurrence
        /// ids, so they sort last in any count-ordered report. 637541791 is the one that owns the three
        /// wired menu buttons. A dead id's reference count is inversely related to how much it matters
        /// here, which is exactly why this baseline records both numbers.
        /// </summary>
        private const string BaselineScene = "Assets/_Project/Scenes/01_MAIN_MENU.unity";
        private const int BaselineOrphanCount = 95;
        private const int BaselineDeadParentCount = 8;

        /// <summary>
        /// Names that live source looks up BY NAME. A by-name miss returns null and the caller returns
        /// without acting, so absence here is silent at runtime and must be reported loudly here.
        /// </summary>
        private static readonly RequiredName[] RequiredNames =
        {
            new RequiredName(
                "Assets/_Project/Scenes/01_MAIN_MENU.unity",
                "H8_MENU_READABLE_OVERLAY_1428",
                "Assets/_Project/Scripts/Editor/MainMenuValidator.cs:24 names this Canvas and " +
                "MainMenuValidator.cs:28 requires ExpectedMenuCanvasCount = 2 serialized WorldSpace " +
                "canvases; only 'Canvas' survives, so ValidateCanvasInventory reports both 'Readable " +
                "overlay Canvas missing' and a WorldSpace count of 1"),
            new RequiredName(
                "Assets/_Project/Scenes/01_MAIN_MENU.unity",
                "H8_MENU_VISUAL_STAGE_1428",
                "Assets/_Project/Scripts/UI/MainMenuAtmosphereController.cs:19 declares this name and " +
                "MainMenuAtmosphereController.cs:148 resolves it. On a miss line 151 falls back to the " +
                "Main Camera's own transform, so every FindChildRecursiveByNameCold at " +
                "MainMenuAtmosphereController.cs:156-171 searches under the camera instead of the " +
                "stage and returns null for all 19 authored Stage_* objects. EnsureBackdropCold and " +
                "EnsureHazeCold then return with the renderer set to null " +
                "(MainMenuAtmosphereController.cs:296-324) and Advance animates nothing"),
        };

        private static readonly Regex AnchorPattern =
            new Regex(@"^--- !u!(-?\d+) &(-?\d+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // A LOCAL reference closes straight after the number, so it carries no guid and no type.
        // {fileID: 11500000, guid: ..., type: 3} deliberately does not match: that resolves through the
        // asset database to another file and is not an intra-scene link.
        private static readonly Regex LocalReferencePattern =
            new Regex(@"\{fileID:\s*(-?\d+)\s*\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ScriptGuidPattern =
            new Regex(@"guid:\s*([0-9a-fA-F]{32})", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly struct RequiredName
        {
            public readonly string ScenePath;
            public readonly string Name;
            public readonly string Requirement;

            public RequiredName(string scenePath, string name, string requirement)
            {
                ScenePath = scenePath;
                Name = name;
                Requirement = requirement;
            }
        }

        private sealed class PersistentCall
        {
            public long TargetAnchor;
            public bool TargetIsExternalAsset;
            public string MethodName;
            public string TargetTypeName;
            public int Line;
        }

        private sealed class Document
        {
            public int ClassId;
            public long Anchor;
            public int StartLine;
            public string Name;
            public long GameObjectAnchor;
            public long FatherAnchor;
            public string ScriptGuid;
            public int IsActive = -1;
            public string TextValue;
            public string LocalPosition;
            public string AnchorMin;
            public string AnchorMax;
            public string SizeDelta;
            public List<long> ComponentAnchors;
            public List<PersistentCall> PersistentCalls;

            public bool IsTransform => ClassId == TransformClassId || ClassId == RectTransformClassId;
        }

        private sealed class SceneModel
        {
            public bool IsText;
            public int LineCount;
            public int DocumentCount;
            public readonly Dictionary<long, Document> ByAnchor = new Dictionary<long, Document>();
            public readonly List<Document> Documents = new List<Document>();
            public readonly Dictionary<long, List<long>> ChildrenByFather = new Dictionary<long, List<long>>();
            public readonly Dictionary<long, List<Document>> OrphansByDeadFather =
                new Dictionary<long, List<Document>>();
            public readonly Dictionary<long, List<string>> NonFatherDeadRefs = new Dictionary<long, List<string>>();
            public readonly HashSet<string> ObjectNames = new HashSet<string>(StringComparer.Ordinal);
            public int OrphanCount;
        }

        [MenuItem(MenuPath)]
        public static void Run()
        {
            string[] scenes = SplitArg("-h8OrphanScenes", DefaultScenes);

            if (!SelfTestPassed())
                return;

            Debug.Log(
                Marker + " START scenes=" + scenes.Length +
                " - no scene is opened, nothing is written, marked dirty or saved.");

            for (int i = 0; i < scenes.Length; i++)
            {
                string scenePath = scenes[i];
                if (!System.IO.File.Exists(scenePath))
                {
                    Debug.LogWarning(Marker + " MISSING SCENE " + scenePath + " - not audited.");
                    continue;
                }

                Debug.Log(Marker + " ===== SCENE " + scenePath + " =====");
                SceneModel model = Parse(scenePath);
                if (!model.IsText)
                {
                    Debug.LogWarning(
                        Marker + "   SKIPPED - " + scenePath + " is BINARY on disk (" + model.LineCount +
                        " lines read, no %YAML header). Every absence this tool could report would be a " +
                        "FALSE ABSENCE, so it reports none. This silence is not a clean bill of health.");
                    continue;
                }

                Report(scenePath, model);
            }

            Debug.Log(Marker + " DONE - no scene was opened and nothing was modified.");
        }

        // ------------------------------------------------------------------------------------------
        // Parse
        // ------------------------------------------------------------------------------------------

        private static SceneModel Parse(string scenePath)
        {
            var model = new SceneModel();

            string[] lines;
            try
            {
                lines = System.IO.File.ReadAllLines(scenePath);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    Marker + " FAILED to read " + scenePath + ": " + ex.GetType().Name + ": " + ex.Message);
                return model;
            }

            model.LineCount = lines.Length;
            model.IsText = lines.Length > 0 && lines[0].StartsWith("%YAML", StringComparison.Ordinal);
            if (!model.IsText)
                return model;

            BuildModel(lines, model);
            return model;
        }

        /// <summary>
        /// Shared by the report and the self-test so the instrument and its proof cannot diverge.
        /// </summary>
        private static void BuildModel(string[] lines, SceneModel model)
        {
            Document current = null;
            bool inComponentList = false;
            PersistentCall pendingCall = null;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                Match anchor = AnchorPattern.Match(line);
                if (anchor.Success)
                {
                    FlushCall(current, pendingCall);
                    pendingCall = null;
                    inComponentList = false;

                    current = new Document
                    {
                        ClassId = (int)ParseLong(anchor.Groups[1].Value),
                        Anchor = ParseLong(anchor.Groups[2].Value),
                        StartLine = i + 1,
                    };
                    model.Documents.Add(current);
                    model.ByAnchor[current.Anchor] = current;
                    continue;
                }

                if (current == null)
                    continue;

                string trimmed = line.Trim();

                if (inComponentList)
                {
                    if (trimmed.StartsWith("- component:", StringComparison.Ordinal))
                    {
                        Match component = LocalReferencePattern.Match(line);
                        if (component.Success)
                        {
                            if (current.ComponentAnchors == null)
                                current.ComponentAnchors = new List<long>(8);
                            current.ComponentAnchors.Add(ParseLong(component.Groups[1].Value));
                        }

                        continue;
                    }

                    inComponentList = false;
                }

                if (string.Equals(trimmed, "m_Component:", StringComparison.Ordinal))
                {
                    inComponentList = true;
                    continue;
                }

                if (current.Name == null && trimmed.StartsWith("m_Name:", StringComparison.Ordinal))
                {
                    current.Name = trimmed.Substring("m_Name:".Length).Trim();
                    if (current.ClassId == GameObjectClassId && current.Name.Length > 0)
                        model.ObjectNames.Add(current.Name);
                }
                else if (current.GameObjectAnchor == 0 &&
                         trimmed.StartsWith("m_GameObject:", StringComparison.Ordinal))
                {
                    Match owner = LocalReferencePattern.Match(line);
                    if (owner.Success)
                        current.GameObjectAnchor = ParseLong(owner.Groups[1].Value);
                }
                else if (current.FatherAnchor == 0 && trimmed.StartsWith("m_Father:", StringComparison.Ordinal))
                {
                    Match father = LocalReferencePattern.Match(line);
                    if (father.Success)
                        current.FatherAnchor = ParseLong(father.Groups[1].Value);
                }
                else if (current.ScriptGuid == null && trimmed.StartsWith("m_Script:", StringComparison.Ordinal))
                {
                    Match guid = ScriptGuidPattern.Match(line);
                    current.ScriptGuid = guid.Success ? guid.Groups[1].Value : string.Empty;
                }
                else if (current.IsActive < 0 && trimmed.StartsWith("m_IsActive:", StringComparison.Ordinal))
                {
                    current.IsActive = trimmed.EndsWith("1", StringComparison.Ordinal) ? 1 : 0;
                }
                else if (current.TextValue == null && trimmed.StartsWith("m_Text:", StringComparison.Ordinal))
                {
                    // Legacy UnityEngine.UI.Text serializes m_Text with a capital T.
                    current.TextValue = trimmed.Substring("m_Text:".Length).Trim();
                }
                else if (current.TextValue == null && trimmed.StartsWith("m_text:", StringComparison.Ordinal))
                {
                    // TMP_Text serializes the LOWERCASE m_text. Matching only m_Text drops every
                    // TextMeshPro label in the file - measured on 01_MAIN_MENU.unity, that is 67 of the
                    // 83 labels, including all five world-space Stage_* texts. A case-insensitive match
                    // is not the fix either: it would also swallow unrelated fields on other types.
                    current.TextValue = trimmed.Substring("m_text:".Length).Trim();
                }
                else if (current.LocalPosition == null &&
                         trimmed.StartsWith("m_LocalPosition:", StringComparison.Ordinal))
                {
                    current.LocalPosition = trimmed.Substring("m_LocalPosition:".Length).Trim();
                }
                else if (current.AnchorMin == null && trimmed.StartsWith("m_AnchorMin:", StringComparison.Ordinal))
                {
                    current.AnchorMin = trimmed.Substring("m_AnchorMin:".Length).Trim();
                }
                else if (current.AnchorMax == null && trimmed.StartsWith("m_AnchorMax:", StringComparison.Ordinal))
                {
                    current.AnchorMax = trimmed.Substring("m_AnchorMax:".Length).Trim();
                }
                else if (current.SizeDelta == null && trimmed.StartsWith("m_SizeDelta:", StringComparison.Ordinal))
                {
                    current.SizeDelta = trimmed.Substring("m_SizeDelta:".Length).Trim();
                }

                // UnityEvent persistent calls. A call is a m_Target line followed by
                // m_TargetAssemblyTypeName and m_MethodName before the next m_Target, so the previous
                // one is flushed when a new target or a new document starts.
                if (trimmed.StartsWith("- m_Target:", StringComparison.Ordinal) ||
                    trimmed.StartsWith("m_Target:", StringComparison.Ordinal))
                {
                    FlushCall(current, pendingCall);
                    Match target = LocalReferencePattern.Match(line);
                    pendingCall = new PersistentCall
                    {
                        Line = i + 1,
                        TargetAnchor = target.Success ? ParseLong(target.Groups[1].Value) : 0L,
                        TargetIsExternalAsset = !target.Success && ScriptGuidPattern.IsMatch(line),
                    };
                }
                else if (pendingCall != null &&
                         trimmed.StartsWith("m_MethodName:", StringComparison.Ordinal))
                {
                    pendingCall.MethodName = trimmed.Substring("m_MethodName:".Length).Trim();
                }
                else if (pendingCall != null &&
                         trimmed.StartsWith("m_TargetAssemblyTypeName:", StringComparison.Ordinal))
                {
                    pendingCall.TargetTypeName = trimmed.Substring("m_TargetAssemblyTypeName:".Length).Trim();
                }
            }

            FlushCall(current, pendingCall);
            model.DocumentCount = model.Documents.Count;

            for (int i = 0; i < model.Documents.Count; i++)
            {
                Document document = model.Documents[i];
                if (!document.IsTransform || document.FatherAnchor == 0)
                    continue;

                if (model.ByAnchor.ContainsKey(document.FatherAnchor))
                {
                    if (!model.ChildrenByFather.TryGetValue(document.FatherAnchor, out List<long> children))
                    {
                        children = new List<long>(4);
                        model.ChildrenByFather[document.FatherAnchor] = children;
                    }

                    children.Add(document.Anchor);
                    continue;
                }

                if (!model.OrphansByDeadFather.TryGetValue(document.FatherAnchor, out List<Document> orphans))
                {
                    orphans = new List<Document>(8);
                    model.OrphansByDeadFather[document.FatherAnchor] = orphans;
                }

                orphans.Add(document);
                model.OrphanCount++;
            }

            CollectNonFatherDeadRefs(lines, model);
        }

        private static void FlushCall(Document owner, PersistentCall call)
        {
            if (owner == null || call == null || call.MethodName == null)
                return;

            if (owner.PersistentCalls == null)
                owner.PersistentCalls = new List<PersistentCall>(4);
            owner.PersistentCalls.Add(call);
        }

        /// <summary>
        /// Every dead local reference that is NOT an m_Father. In 01_MAIN_MENU there are none - all 95
        /// are parent links - but a dead m_Script, m_Camera or serialized field reference is a
        /// different defect with a different repair, so it must not be silently folded into the orphan
        /// count. Reporting zero here is the evidence that the orphan model covers the whole finding.
        /// </summary>
        private static void CollectNonFatherDeadRefs(string[] lines, SceneModel model)
        {
            int cursor = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (AnchorPattern.IsMatch(line))
                {
                    cursor++;
                    continue;
                }

                if (line.IndexOf("fileID:", StringComparison.Ordinal) < 0)
                    continue;

                string trimmed = line.Trim();
                bool isFather = trimmed.StartsWith("m_Father:", StringComparison.Ordinal);

                foreach (Match reference in LocalReferencePattern.Matches(line))
                {
                    long target = ParseLong(reference.Groups[1].Value);
                    if (target == 0 || model.ByAnchor.ContainsKey(target))
                        continue;

                    if (isFather)
                        continue;

                    Document owner = cursor >= 0 && cursor < model.Documents.Count
                        ? model.Documents[cursor]
                        : null;

                    if (!model.NonFatherDeadRefs.TryGetValue(target, out List<string> entries))
                    {
                        entries = new List<string>(4);
                        model.NonFatherDeadRefs[target] = entries;
                    }

                    entries.Add(
                        trimmed + " in " + DescribeClassId(owner != null ? owner.ClassId : 0) +
                        " anchor " + (owner != null ? owner.Anchor : 0L) + " at line " + (i + 1));
                }
            }
        }

        // ------------------------------------------------------------------------------------------
        // Report
        // ------------------------------------------------------------------------------------------

        private static void Report(string scenePath, SceneModel model)
        {
            Debug.Log(
                Marker + "   documents=" + model.DocumentCount + " lines=" + model.LineCount +
                " orphanedTransforms=" + model.OrphanCount +
                " deadParentIds=" + model.OrphansByDeadFather.Count);

            ReportBaseline(scenePath, model);
            ReportRequiredNames(scenePath, model);
            ReportNonFatherDeadRefs(model);

            if (model.OrphansByDeadFather.Count == 0)
            {
                Debug.Log(
                    Marker + "   no orphaned transforms - every m_Father resolves to an anchor in this file.");
                return;
            }

            var groups = new List<KeyValuePair<long, List<Document>>>(model.OrphansByDeadFather);
            groups.Sort(CompareBySubtreeSizeDescending(model));

            int totalSubtree = 0;
            int totalWiredCalls = 0;
            for (int i = 0; i < groups.Count; i++)
            {
                totalSubtree += ReportGroup(scenePath, model, groups[i].Key, groups[i].Value, ref totalWiredCalls);
            }

            Debug.LogError(
                Marker + "   VERDICT " + model.OrphanCount + " transform(s) hold a dead m_Father across " +
                model.OrphansByDeadFather.Count + " absent parent(s), dragging " + totalSubtree +
                " object(s) out of the hierarchy, of which " + totalWiredCalls +
                " persistent UnityEvent call(s) survive on objects that cannot be drawn or clicked. " +
                "The absent parents are the finding; the orphans are intact. Repair is an authoring " +
                "decision and this tool makes none.");
        }

        private static Comparison<KeyValuePair<long, List<Document>>> CompareBySubtreeSizeDescending(
            SceneModel model)
        {
            // Cold editor code: one delegate allocated per run, outside any tick cadence.
            return (a, b) =>
            {
                int sizeA = CountSubtree(model, a.Value);
                int sizeB = CountSubtree(model, b.Value);
                return sizeB != sizeA ? sizeB.CompareTo(sizeA) : a.Key.CompareTo(b.Key);
            };
        }

        private static int ReportGroup(
            string scenePath,
            SceneModel model,
            long deadParent,
            List<Document> orphans,
            ref int totalWiredCalls)
        {
            var subtree = new List<Document>(64);
            for (int i = 0; i < orphans.Count; i++)
                CollectSubtree(model, orphans[i].Anchor, subtree);

            int rectTransforms = 0;
            int plainTransforms = 0;
            int canvasRenderers = 0;
            int meshRenderers = 0;
            int inactive = 0;
            var scriptCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var labels = new List<string>(16);
            var wired = new List<string>(8);

            for (int i = 0; i < subtree.Count; i++)
            {
                Document node = subtree[i];
                if (node.ClassId == RectTransformClassId)
                    rectTransforms++;
                else
                    plainTransforms++;

                Document gameObject = ResolveGameObject(model, node);
                if (gameObject != null && gameObject.IsActive == 0)
                    inactive++;

                if (gameObject == null || gameObject.ComponentAnchors == null)
                    continue;

                for (int c = 0; c < gameObject.ComponentAnchors.Count; c++)
                {
                    if (!model.ByAnchor.TryGetValue(gameObject.ComponentAnchors[c], out Document component))
                        continue;

                    if (component.ClassId == CanvasRendererClassId)
                        canvasRenderers++;
                    else if (component.ClassId == MeshRendererClassId)
                        meshRenderers++;

                    string typeName = DescribeComponent(component);
                    scriptCounts.TryGetValue(typeName, out int count);
                    scriptCounts[typeName] = count + 1;

                    if (!string.IsNullOrEmpty(component.TextValue))
                        labels.Add(NameOf(model, node) + " [" + typeName + "] = " + component.TextValue);

                    if (component.PersistentCalls == null)
                        continue;

                    for (int p = 0; p < component.PersistentCalls.Count; p++)
                    {
                        PersistentCall call = component.PersistentCalls[p];
                        wired.Add(DescribeCall(model, node, typeName, call, scenePath));
                    }
                }
            }

            totalWiredCalls += wired.Count;

            Debug.LogWarning(
                Marker + "     ABSENT PARENT id " + deadParent + "  directOrphans=" + orphans.Count +
                "  subtreeObjects=" + subtree.Count + "  rectTransforms=" + rectTransforms +
                "  plainTransforms=" + plainTransforms + "  canvasRenderers=" + canvasRenderers +
                "  meshRenderers=" + meshRenderers + "  inactiveObjects=" + inactive);

            Debug.Log(Marker + "       what the absent parent WAS, from the evidence: " +
                      Infer(orphans, rectTransforms, plainTransforms, canvasRenderers, meshRenderers, labels.Count));

            Debug.Log(Marker + "       components in subtree: " + Summarize(scriptCounts, 10));

            int printedOrphans = Math.Min(orphans.Count, MaxDirectOrphansPrintedPerParent);
            for (int i = 0; i < printedOrphans; i++)
            {
                Document orphan = orphans[i];
                var line = new StringBuilder(160);
                line.Append(Marker).Append("       direct orphan '").Append(NameOf(model, orphan));
                line.Append("' (").Append(DescribeClassId(orphan.ClassId)).Append(" anchor ");
                line.Append(orphan.Anchor).Append(") at ").Append(scenePath).Append(':');
                line.Append(orphan.StartLine);
                if (orphan.LocalPosition != null)
                    line.Append("  localPos=").Append(orphan.LocalPosition);
                if (orphan.ClassId == RectTransformClassId && orphan.AnchorMin != null)
                {
                    line.Append("  anchorMin=").Append(orphan.AnchorMin);
                    line.Append(" anchorMax=").Append(orphan.AnchorMax ?? "?");
                    line.Append(" sizeDelta=").Append(orphan.SizeDelta ?? "?");
                }

                int subtreeSize = CountSubtree(model, orphan.Anchor);
                if (subtreeSize > 1)
                    line.Append("  carries ").Append(subtreeSize - 1).Append(" descendant(s)");

                Debug.Log(line.ToString());

                if (subtreeSize > 1)
                {
                    int budget = MaxSubtreeNodesPrintedPerOrphan;
                    PrintSubtree(model, orphan.Anchor, 0, ref budget);

                    // Descendants printed is subtreeSize - 1, so compare against that rather than
                    // against a spent budget: a subtree of exactly the budget size is not truncated.
                    if (subtreeSize - 1 > MaxSubtreeNodesPrintedPerOrphan)
                    {
                        Debug.Log(
                            Marker + "         ...descendant listing truncated at " +
                            MaxSubtreeNodesPrintedPerOrphan + " node(s); subtreeObjects above is the count.");
                    }
                }
            }

            if (orphans.Count > printedOrphans)
            {
                Debug.Log(
                    Marker + "       ...+" + (orphans.Count - printedOrphans) +
                    " more direct orphan(s) not printed.");
            }

            for (int i = 0; i < labels.Count; i++)
                Debug.Log(Marker + "       authored text: " + labels[i]);

            for (int i = 0; i < wired.Count; i++)
                Debug.LogError(Marker + "       " + wired[i]);

            return subtree.Count;
        }

        private static string DescribeCall(
            SceneModel model,
            Document owner,
            string typeName,
            PersistentCall call,
            string scenePath)
        {
            string target;
            if (call.TargetAnchor == 0)
            {
                target = call.TargetIsExternalAsset ? "<asset outside this scene>" : "<null target>";
            }
            else if (model.ByAnchor.TryGetValue(call.TargetAnchor, out Document targetDocument))
            {
                target = "'" + NameOf(model, targetDocument) + "' (" +
                         DescribeComponent(targetDocument) + " anchor " + call.TargetAnchor +
                         ") which IS STILL ALIVE in this scene";
            }
            else
            {
                target = "anchor " + call.TargetAnchor + " which is ALSO DEAD";
            }

            return "UNREACHABLE WIRED CALL '" + NameOf(model, owner) + "' [" + typeName + "] -> " +
                   (call.MethodName ?? "<no method>") + " on " + target +
                   (string.IsNullOrEmpty(call.TargetTypeName) ? string.Empty : "  declaredType=" + call.TargetTypeName) +
                   " at " + scenePath + ":" + call.Line +
                   " - the wiring is intact and this control can never fire.";
        }

        /// <summary>
        /// Reads the component mix back into a statement about the deleted parent. It is an inference
        /// and it is labelled as one; the counts above it are the measurement.
        /// </summary>
        private static string Infer(
            List<Document> orphans,
            int rectTransforms,
            int plainTransforms,
            int canvasRenderers,
            int meshRenderers,
            int labelCount)
        {
            var builder = new StringBuilder(320);

            bool allRect = true;
            for (int i = 0; i < orphans.Count; i++)
            {
                if (orphans[i].ClassId != RectTransformClassId)
                {
                    allRect = false;
                    break;
                }
            }

            builder.Append(allRect
                ? "every direct orphan is a RectTransform, so the absent parent carried a RectTransform - a Canvas or a UI container. "
                : "at least one direct orphan is a plain Transform, so the absent parent was a plain Transform world-space group root. ");

            if (canvasRenderers > 0 && meshRenderers == 0)
            {
                builder.Append(canvasRenderers).Append(
                    " CanvasRenderer(s) draw only through a Canvas ancestor. Promoted to scene roots they have none, so this whole subtree is invisible and unraycastable. ");
            }
            else if (meshRenderers > 0 && canvasRenderers == 0)
            {
                builder.Append(meshRenderers).Append(
                    " MeshRenderer(s) need no Canvas and STILL RENDER as roots - the loss here is not visibility but ownership: whatever resolved this parent by name now resolves null. ");
            }
            else if (meshRenderers > 0 && canvasRenderers > 0)
            {
                builder.Append("the subtree mixes ").Append(meshRenderers).Append(" MeshRenderer(s), which still render as roots, with ")
                    .Append(canvasRenderers).Append(" CanvasRenderer(s), which cannot. ");
            }

            if (labelCount > 0)
                builder.Append("Authored text is printed below and identifies it.");
            else
                builder.Append("No authored text in this subtree, so identity rests on the child names.");

            builder.Append(" rect=").Append(rectTransforms).Append(" plain=").Append(plainTransforms);
            return builder.ToString();
        }

        private static void ReportBaseline(string scenePath, SceneModel model)
        {
            if (!string.Equals(scenePath, BaselineScene, StringComparison.OrdinalIgnoreCase))
                return;

            if (model.OrphanCount == BaselineOrphanCount &&
                model.OrphansByDeadFather.Count == BaselineDeadParentCount)
            {
                Debug.Log(
                    Marker + "     baseline MATCHED " + BaselineOrphanCount + " orphan(s) from " +
                    BaselineDeadParentCount + " absent parent(s), as measured 2026-07-28. The editor " +
                    "reported the same 95 'Broken text PPtr' instances, so this parse reproduces the " +
                    "engine's own count. Note the distinct-id figure: EIGHT, not the six that a hand " +
                    "reading of the console log produced.");
                return;
            }

            Debug.LogError(
                Marker + "     baseline CHANGED measured orphans=" + model.OrphanCount + " (baseline " +
                BaselineOrphanCount + ") absentParents=" + model.OrphansByDeadFather.Count +
                " (baseline " + BaselineDeadParentCount + "). Either the scene was edited or this parser " +
                "regressed. Resolve which before quoting any number below.");
        }

        private static void ReportRequiredNames(string scenePath, SceneModel model)
        {
            for (int i = 0; i < RequiredNames.Length; i++)
            {
                RequiredName required = RequiredNames[i];
                if (!string.Equals(required.ScenePath, scenePath, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (model.ObjectNames.Contains(required.Name))
                {
                    Debug.Log(Marker + "     REQUIRED NAME PRESENT '" + required.Name + "'");
                    continue;
                }

                Debug.LogError(
                    Marker + "     REQUIRED NAME ABSENT '" + required.Name + "' - not one of the " +
                    model.ObjectNames.Count + " GameObject names in this text scene. " +
                    required.Requirement +
                    ". The lookup is by name and fails silently: nothing throws and nothing works.");
            }
        }

        private static void ReportNonFatherDeadRefs(SceneModel model)
        {
            if (model.NonFatherDeadRefs.Count == 0)
            {
                Debug.Log(
                    Marker + "     every dead local reference in this file is an m_Father, so the orphan " +
                    "model below covers the whole finding - there is no separate dead-field family.");
                return;
            }

            Debug.LogWarning(
                Marker + "     " + model.NonFatherDeadRefs.Count +
                " dead local reference(s) are NOT m_Father. These are a different defect with a " +
                "different repair and are not counted as orphans:");

            foreach (KeyValuePair<long, List<string>> entry in model.NonFatherDeadRefs)
            {
                for (int i = 0; i < entry.Value.Count; i++)
                    Debug.LogWarning(Marker + "       dead id " + entry.Key + ": " + entry.Value[i]);
            }
        }

        // ------------------------------------------------------------------------------------------
        // Hierarchy helpers
        // ------------------------------------------------------------------------------------------

        private static void CollectSubtree(SceneModel model, long anchor, List<Document> into)
        {
            if (!model.ByAnchor.TryGetValue(anchor, out Document node))
                return;

            into.Add(node);
            if (!model.ChildrenByFather.TryGetValue(anchor, out List<long> children))
                return;

            for (int i = 0; i < children.Count; i++)
            {
                // m_Father cannot form a cycle without the same anchor appearing twice as a document,
                // which the anchor dictionary makes impossible, so no visited set is needed.
                if (children[i] != anchor)
                    CollectSubtree(model, children[i], into);
            }
        }

        private static int CountSubtree(SceneModel model, long anchor)
        {
            int count = 1;
            if (!model.ChildrenByFather.TryGetValue(anchor, out List<long> children))
                return count;

            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] != anchor)
                    count += CountSubtree(model, children[i]);
            }

            return count;
        }

        private static int CountSubtree(SceneModel model, List<Document> orphans)
        {
            int count = 0;
            for (int i = 0; i < orphans.Count; i++)
                count += CountSubtree(model, orphans[i].Anchor);

            return count;
        }

        /// <summary>
        /// The budget counts NODES, not depth. A depth budget would look like a limit and then print an
        /// unbounded number of siblings, which is how a diagnostic floods a log on the one scene where
        /// it matters most.
        /// </summary>
        private static void PrintSubtree(SceneModel model, long anchor, int depth, ref int budget)
        {
            if (!model.ByAnchor.TryGetValue(anchor, out Document node))
                return;

            if (depth > 0)
            {
                if (budget <= 0)
                    return;

                budget--;
                var line = new StringBuilder(160);
                line.Append(Marker).Append("         ");
                for (int i = 0; i < depth; i++)
                    line.Append("  ");
                line.Append("- ").Append(NameOf(model, node)).Append(" [").Append(DescribeComponents(model, node))
                    .Append(']');
                Debug.Log(line.ToString());
            }

            if (!model.ChildrenByFather.TryGetValue(anchor, out List<long> children))
                return;

            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] != anchor)
                    PrintSubtree(model, children[i], depth + 1, ref budget);
            }
        }

        private static Document ResolveGameObject(SceneModel model, Document node)
        {
            if (node.ClassId == GameObjectClassId)
                return node;

            return node.GameObjectAnchor != 0 &&
                   model.ByAnchor.TryGetValue(node.GameObjectAnchor, out Document gameObject)
                ? gameObject
                : null;
        }

        private static string NameOf(SceneModel model, Document node)
        {
            Document gameObject = ResolveGameObject(model, node);
            if (gameObject != null && !string.IsNullOrEmpty(gameObject.Name))
                return gameObject.Name;

            return string.IsNullOrEmpty(node.Name) ? "<unnamed>" : node.Name;
        }

        private static string DescribeComponents(SceneModel model, Document node)
        {
            Document gameObject = ResolveGameObject(model, node);
            if (gameObject == null || gameObject.ComponentAnchors == null)
                return "no components";

            var builder = new StringBuilder(96);
            for (int i = 0; i < gameObject.ComponentAnchors.Count; i++)
            {
                if (builder.Length > 0)
                    builder.Append(", ");

                if (model.ByAnchor.TryGetValue(gameObject.ComponentAnchors[i], out Document component))
                    builder.Append(DescribeComponent(component));
                else
                    builder.Append("MISSING COMPONENT ").Append(gameObject.ComponentAnchors[i]);
            }

            return builder.Length == 0 ? "no components" : builder.ToString();
        }

        /// <summary>
        /// A MonoBehaviour's real type comes from resolving its m_Script guid through the asset
        /// database, not from a hardcoded table that would rot. An unresolvable guid is reported as
        /// such, because "missing script asset" is itself a defect and must not print as a class name.
        /// </summary>
        private static string DescribeComponent(Document component)
        {
            if (component.ClassId != MonoBehaviourClassId)
                return DescribeClassId(component.ClassId);

            if (string.IsNullOrEmpty(component.ScriptGuid))
                return "MonoBehaviour<no m_Script guid>";

            string path = AssetDatabase.GUIDToAssetPath(component.ScriptGuid);
            if (string.IsNullOrEmpty(path))
                return "MonoBehaviour<UNRESOLVED SCRIPT GUID " + component.ScriptGuid + ">";

            int slash = path.LastIndexOf('/');
            string file = slash >= 0 ? path.Substring(slash + 1) : path;
            int dot = file.LastIndexOf('.');
            return dot > 0 ? file.Substring(0, dot) : file;
        }

        private static string DescribeClassId(int classId)
        {
            switch (classId)
            {
                case 0: return "unattributed";
                case GameObjectClassId: return "GameObject";
                case TransformClassId: return "Transform";
                case 20: return "Camera";
                case MeshRendererClassId: return "MeshRenderer";
                case 33: return "MeshFilter";
                case 65: return "BoxCollider";
                case 108: return "Light";
                case MonoBehaviourClassId: return "MonoBehaviour";
                case CanvasRendererClassId: return "CanvasRenderer";
                case CanvasClassId: return "Canvas";
                case RectTransformClassId: return "RectTransform";
                case 225: return "CanvasGroup";
                case 1001: return "PrefabInstance";
                case 1660057539: return "SceneRoots";
                default: return "classId=" + classId;
            }
        }

        // ------------------------------------------------------------------------------------------
        // Self-test and helpers
        // ------------------------------------------------------------------------------------------

        /// <summary>
        /// Known-answer cases on an embedded sample, run before anything is printed. The four claims
        /// this tool rests on are all checked: a guid-bearing brace group is not a local reference, an
        /// absent anchor makes an orphan, a grandchild is pulled into the orphaned subtree, and a
        /// persistent call is attributed with its target and method. A failure suppresses the report,
        /// because a wrong subtree size would misdirect the repair.
        /// </summary>
        private static bool SelfTestPassed()
        {
            string[] sample =
            {
                "%YAML 1.1",
                "%TAG !u! tag:unity3d.com,2011:",
                "--- !u!1 &100",
                "GameObject:",
                "  serializedVersion: 6",
                "  m_Component:",
                "  - component: {fileID: 200}",
                "  - component: {fileID: 300}",
                "  m_Name: OrphanRoot",
                "  m_IsActive: 1",
                "--- !u!224 &200",
                "RectTransform:",
                "  m_GameObject: {fileID: 100}",
                "  m_LocalPosition: {x: 1, y: 2, z: 3}",
                "  m_Children:",
                "  - {fileID: 500}",
                "  m_Father: {fileID: 4242}",
                "  m_AnchorMin: {x: 0.1, y: 0.2}",
                "  m_AnchorMax: {x: 0.3, y: 0.4}",
                "  m_SizeDelta: {x: 0, y: 0}",
                "--- !u!114 &300",
                "MonoBehaviour:",
                "  m_GameObject: {fileID: 100}",
                "  m_Script: {fileID: 11500000, guid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, type: 3}",
                "  m_OnClick:",
                "    m_PersistentCalls:",
                "      m_Calls:",
                "      - m_Target: {fileID: 600}",
                "        m_TargetAssemblyTypeName: Some.Controller, Some.Assembly",
                "        m_MethodName: StartGame",
                "        m_Mode: 1",
                "--- !u!1 &400",
                "GameObject:",
                "  serializedVersion: 6",
                "  m_Component:",
                "  - component: {fileID: 500}",
                "  m_Name: OrphanChild",
                "  m_IsActive: 1",
                "--- !u!224 &500",
                "RectTransform:",
                "  m_GameObject: {fileID: 400}",
                "  m_Father: {fileID: 200}",
                "--- !u!1 &600",
                "GameObject:",
                "  m_Name: LiveTarget",
                "  m_IsActive: 1",
                "--- !u!4 &700",
                "Transform:",
                "  m_GameObject: {fileID: 600}",
                "  m_Father: {fileID: 0}",
            };

            var model = new SceneModel { IsText = true };
            BuildModel(sample, model);

            if (model.OrphansByDeadFather.Count != 1 || model.OrphanCount != 1)
            {
                Debug.LogError(
                    Marker + " SELF-TEST FAILED expected exactly 1 orphan from 1 absent parent (4242); " +
                    "measured orphans=" + model.OrphanCount + " absentParents=" +
                    model.OrphansByDeadFather.Count + ". Either the guid-bearing m_Script reference or " +
                    "{fileID: 0} was miscounted as a dead parent. Report suppressed.");
                return false;
            }

            if (!model.OrphansByDeadFather.TryGetValue(4242L, out List<Document> orphans) ||
                orphans.Count != 1 ||
                !string.Equals(NameOf(model, orphans[0]), "OrphanRoot", StringComparison.Ordinal))
            {
                Debug.LogError(
                    Marker + " SELF-TEST FAILED the dead parent 4242 did not attribute to 'OrphanRoot', " +
                    "so owner naming cannot be trusted. Report suppressed.");
                return false;
            }

            int subtree = CountSubtree(model, orphans[0].Anchor);
            if (subtree != 2)
            {
                Debug.LogError(
                    Marker + " SELF-TEST FAILED subtree of the orphan measured " + subtree +
                    " object(s); 'OrphanChild' hangs off it via m_Father so the answer is 2. Subtree " +
                    "reconstruction is the whole point of this tool. Report suppressed.");
                return false;
            }

            if (model.NonFatherDeadRefs.Count != 0)
            {
                Debug.LogError(
                    Marker + " SELF-TEST FAILED the clean sample reported " + model.NonFatherDeadRefs.Count +
                    " non-m_Father dead reference(s); it has none. False positives. Report suppressed.");
                return false;
            }

            if (!model.ByAnchor.TryGetValue(300L, out Document behaviour) ||
                behaviour.PersistentCalls == null ||
                behaviour.PersistentCalls.Count != 1 ||
                !string.Equals(behaviour.PersistentCalls[0].MethodName, "StartGame", StringComparison.Ordinal) ||
                behaviour.PersistentCalls[0].TargetAnchor != 600L)
            {
                Debug.LogError(
                    Marker + " SELF-TEST FAILED the persistent onClick call did not extract as " +
                    "StartGame on anchor 600, so no wiring claim can be trusted. Report suppressed.");
                return false;
            }

            var negative = new SceneModel { IsText = true };
            BuildModel(
                new[]
                {
                    "%YAML 1.1",
                    "--- !u!1 &100",
                    "GameObject:",
                    "  m_Name: Clean",
                    "--- !u!4 &200",
                    "Transform:",
                    "  m_GameObject: {fileID: 100}",
                    "  m_Father: {fileID: 0}",
                },
                negative);

            if (negative.OrphanCount != 0 || negative.OrphansByDeadFather.Count != 0)
            {
                Debug.LogError(
                    Marker + " SELF-TEST FAILED a clean sample reported " + negative.OrphanCount +
                    " orphan(s). The reporter produces false positives. Report suppressed.");
                return false;
            }

            Debug.Log(
                Marker + " SELF-TEST PASSED 1 absent parent found with correct owner name, a 2-object " +
                "subtree reconstructed through m_Father, the persistent onClick extracted as StartGame " +
                "on a live anchor, guid-bearing and {fileID: 0} references ignored, clean sample clean.");
            return true;
        }

        private static long ParseLong(string value)
        {
            return long.TryParse(value, out long parsed) ? parsed : 0L;
        }

        private static string Summarize(Dictionary<string, int> counts, int take)
        {
            var list = new List<KeyValuePair<string, int>>(counts);
            list.Sort((a, b) => b.Value != a.Value
                ? b.Value.CompareTo(a.Value)
                : string.Compare(a.Key, b.Key, StringComparison.Ordinal));

            var builder = new StringBuilder(160);
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

            // StringSplitOptions.TrimEntries is .NET 5; this compiles against netstandard2.1.
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
