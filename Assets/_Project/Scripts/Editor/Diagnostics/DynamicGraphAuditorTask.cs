using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hecton8.Editor.Diagnostics
{
    /// <summary>
    /// Reflection audit of the sandbox MapMagic graph: node ids, node types, a few authored parameter
    /// values, and which outlet feeds each inlet. Entry point <see cref="Audit"/> is invoked by reflection
    /// name from Tools/BatchTasks/run_graph_dynamic.bat, so it must not be renamed.
    ///
    /// NO GPU REFUSAL GUARD HERE, ON PURPOSE. The guard in .claude/rules/hecton8-shaders-compute.md:36-37
    /// exists because compute shaders and Graphics.Blit return zeros with no GPU context. This tool never
    /// touches the graphics device: it calls AssetDatabase.LoadAssetAtPath, reads managed fields through
    /// System.Reflection, and writes a text file. No RenderTexture, no Graphics.Blit, no
    /// ComputeShader.Dispatch, no ReadPixels, no EncodeToPNG, and no MapMagic generation (it never calls
    /// StartGenerate, so no matrix is ever produced). run_graph_dynamic.bat correctly does not pass
    /// -nographics. Adding an Exit(3) refusal here would brick a tool that has nothing to be wrong about.
    ///
    /// It is an AUDITOR, which sets the exit-code contract: 0 means the audit ran and every fact it claims
    /// to report was actually read. Anything less is non-zero, because a report that quietly omits edges is
    /// worse than no report - it gets cited as proof that the graph is wired.
    /// </summary>
    public static class DynamicGraphAuditorTask
    {
        private const string GraphAssetPath =
            "Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset";

        /// <summary>
        /// Which graph this run audits. Defaults to the biomes graph above, so every existing caller
        /// (Tools/BatchTasks/run_graph_dynamic.bat) keeps auditing exactly what it audited before.
        ///
        /// WHY THIS EXISTS. The sandbox has TWO graphs and this auditor could only ever see one of them:
        /// the 500-node biomes graph. The 16-node HECTON_PROCEDURAL_GEOLOGY_GRAPH - the geology bench, the
        /// one whose height output is currently under investigation - was unreachable by the only tool in
        /// the repo that can report which outlet feeds which inlet. Hardcoding the second path as a second
        /// const would have produced two auditors that drift apart.
        ///
        /// Passed as `-graphAsset &lt;path&gt;` on the Unity command line. An unrecognised or empty value is
        /// NOT silently replaced by the default: a run asked to audit graph B must never publish a report
        /// about graph A, because the report is then cited as evidence about the wrong asset.
        /// </summary>
        private static string ResolveGraphAssetPath(out string failure)
        {
            failure = null;
            // GLOBALLY QUALIFIED, and it has to be: this project declares a Hecton8.Environment namespace,
            // and inside namespace Hecton8.Editor.Diagnostics the name `Environment` binds to THAT, not to
            // System.Environment. The unqualified form fails with CS0234 "GetCommandLineArgs does not exist
            // in the namespace Hecton8.Environment" - and Unity still exited 0 on that failed compile, so
            // the run looked like a clean audit that simply produced no file.
            string[] args = global::System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-graphAsset", StringComparison.Ordinal))
                    continue;

                if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]))
                {
                    failure =
                        "-graphAsset was passed with no path after it. Refusing to fall back to the " +
                        $"default '{GraphAssetPath}', because a report about the wrong graph is worse " +
                        "than no report.";
                    return null;
                }

                return args[i + 1].Trim();
            }

            return GraphAssetPath;
        }

        // Was C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-...\graph_dump_dynamic.md - another agent's
        // private scratch directory: outside the repo, unversioned, never created by this tool, and
        // invisible to anyone auditing this project's graph evidence. The subfolder is per-tool because
        // several diagnostics here write generically named files and two of them already overwrote each
        // other's evidence inside a single shared directory. `static readonly` rather than `const` because
        // Path.Combine is not a compile-time constant (a `const` here is CS0133).
        private static readonly string OutputDir =
            Path.Combine(Directory.GetCurrentDirectory(), "Logs", "dynamic_graph_audit");

        /// <summary>
        /// Report path for one specific graph. A single fixed filename was correct while exactly one graph
        /// could be audited; now that -graphAsset selects between the 500-node biomes graph and the 16-node
        /// geology graph, one filename means the second run silently overwrites the first run's evidence
        /// with a document whose header names a different asset. The header note on OutputDir records that
        /// two diagnostics in this folder already did that to each other once.
        ///
        /// The old `ReportPath` field is GONE rather than left in place: once every call site took the
        /// per-graph path, it was an unreferenced constant that still looked authoritative, and a later
        /// edit reaching for the "existing" report path would have quietly restored the overwrite.
        /// </summary>
        private static string ReportPathFor(string graphAssetPath) =>
            Path.Combine(OutputDir, $"graph_dump_{Path.GetFileNameWithoutExtension(graphAssetPath)}.md");

        private const BindingFlags AnyInstance =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Fields the audit is asked to publish per node type. Names that the type does not declare are
        // printed as ABSENT rather than skipped - see DumpRequestedFields. Two entries below are known
        // absent today and that is exactly what the report has to say out loud:
        //   trenchMultiplier - HectonSandboxAbyssalShelfMapMagicNode.cs:47-90 declares no such field
        //                      (trenchDepthMeters :63, trenchWidthMeters :64, trenchSharpness :65 exist).
        //   algorithm        - Blend200 keeps it PER LAYER (MatrixModifiers.cs:261
        //                      Blend200.Layer.algorithm), not on the node, so the node-level lookup can
        //                      never find it.
        private static readonly string[] ShelfNodeFields =
            { "ridgeHeightMeters", "ridgeMultiplier", "trenchDepthMeters", "trenchMultiplier" };

        private static readonly string[] LevelsFields =
            { "inMin", "inMax", "gamma", "outMin", "outMax" };

        private static readonly string[] BlendFields = { "algorithm" };

        [MenuItem("Hecton8/Diagnostics/Dynamic Graph Audit")]
        public static void Audit()
        {
            string graphAssetPath = ResolveGraphAssetPath(out string argFailure);
            if (graphAssetPath == null)
            {
                Debug.LogError($"[DynamicGraphAuditorTask] REFUSED: {argFailure} No report was written.");
                EditorApplication.Exit(2);
                return;
            }

            try
            {
                Directory.CreateDirectory(OutputDir);
                DoAudit(graphAssetPath);
            }
            catch (Exception ex)
            {
                // There was no catch at all before. File.WriteAllText pointed at a foreign directory this
                // tool never created, so a DirectoryNotFoundException escaped the method with no line
                // saying which artifact was missing.
                Debug.LogError(
                    $"[DynamicGraphAuditorTask] FAILED, no trustworthy graph audit was produced: {ex}");
                EditorApplication.Exit(2);
                return;
            }

            // Reached only when DoAudit wrote the report AND had no unreadable facts to declare.
            EditorApplication.Exit(0);
        }

        private static void DoAudit(string graphAssetPath)
        {
            // Local, not a field: two audits of two different graphs must not be able to see each other's
            // report path, and the name is deliberately not the same as the GraphAssetPath const so a
            // reader can tell the requested graph from the default one.
            string reportPath = ReportPathFor(graphAssetPath);

            Object graphObj = AssetDatabase.LoadAssetAtPath<Object>(graphAssetPath);
            if (graphObj == null)
            {
                // Was Debug.LogError("Could not find graph") + Exit(1): non-zero, but it named neither the
                // path it looked at nor the artifact that did not appear, and 1 is outside this project's
                // diagnostic exit-code set (0 proved / 2 failed / 3 no GPU / 4 timeout).
                throw new InvalidOperationException(
                    $"the graph asset '{graphAssetPath}' did not load, so there was no graph to audit and " +
                    $"no report was written to {reportPath}.");
            }

            Type graphType = graphObj.GetType();

            FieldInfo genField = graphType.GetField("generators", AnyInstance);
            if (genField == null)
            {
                // THE BUG THIS FILE EXISTED TO DEMONSTRATE. The old code appended "Could not find
                // 'generators' field in Graph." to the report, fell straight through to File.WriteAllText,
                // and called EditorApplication.Exit(0). A run that reached ZERO nodes therefore published a
                // header-plus-one-sentence document and told its caller the audit had succeeded.
                throw new InvalidOperationException(
                    $"'{graphType.FullName}' loaded from '{graphAssetPath}' has no instance field named " +
                    "'generators', so not one node could be reached. Expected " +
                    "MapMagic.Nodes.Graph.generators (Assets/MapMagic/Nodes/Graph.cs:22). No report was " +
                    "written; an audit that enumerated nothing is not a pass.");
            }

            object genValue = genField.GetValue(graphObj);
            if (genValue == null)
            {
                // Graph.generators is [NonSerialized] and repopulated by the deserialization callback
                // (Graph.cs:22). If that callback did not run, the field is null and the old code's
                // `if (generators != null)` skipped the entire loop in total silence - no log line, no note
                // in the report - then wrote a header-only file and exited 0.
                throw new InvalidOperationException(
                    $"'{graphType.FullName}.generators' read as null from '{graphAssetPath}'. The graph's " +
                    "deserialization callback did not populate it, so there are no nodes to audit.");
            }

            IEnumerable generators = genValue as IEnumerable;
            if (generators == null)
            {
                throw new InvalidOperationException(
                    $"'{graphType.FullName}.generators' is a {genValue.GetType().FullName}, which is not " +
                    "IEnumerable, so the node list could not be walked. The old code's `as IEnumerable` " +
                    "cast produced null here and the audit exited 0 having read nothing.");
            }

            // Graph.GetLink(IInlet<object>) is declared once (Graph.cs:390) and takes the INTERFACE, so the
            // old `GetMethod("GetLink", new Type[] { concreteInletType })` lookup never matched and every
            // link went through the by-name fallback anyway. Resolved once by name+arity instead of by
            // signature: GetMethod(string, BindingFlags) throws AmbiguousMatchException the day MapMagic
            // adds an overload, and the old empty `catch {}` would have eaten that as "no links".
            MethodInfo getLinkMethod = null;
            foreach (MethodInfo mi in graphType.GetMethods(AnyInstance))
            {
                if (mi.Name != "GetLink") continue;
                if (mi.GetParameters().Length != 1) continue;
                getLinkMethod = mi;
                break;
            }

            List<string> unreadable = new List<string>();
            if (getLinkMethod == null)
            {
                unreadable.Add(
                    $"'{graphType.FullName}' exposes no single-argument GetLink method, so NOT ONE inlet " +
                    "link in this report was resolved and the link tree is empty for a reason that has " +
                    "nothing to do with the graph");
            }

            StringBuilder body = new StringBuilder();
            int nodeCount = 0;
            int linksResolved = 0;
            int inletsInspected = 0;
            int absentFields = 0;

            foreach (object gen in generators)
            {
                nodeCount++;

                if (gen == null)
                {
                    body.AppendLine($"Node #{nodeCount}: NULL entry in the generators array.");
                    body.AppendLine();
                    unreadable.Add($"generators[{nodeCount - 1}] is null: no type, no id, no links");
                    continue;
                }

                Type t = gen.GetType();

                // A missing 'id' used to print as "Node ID: " with an empty value and no complaint, so
                // reflection drift after a MapMagic upgrade would produce an id-less audit that still
                // exited 0. Generator.id is a public ulong field (Generator.cs:233).
                string idText;
                FieldInfo idField = t.GetField("id", AnyInstance);
                if (idField == null)
                {
                    idText = "UNREADABLE (no 'id' field)";
                    unreadable.Add(
                        $"node #{nodeCount} of type '{t.FullName}' has no 'id' field, so it cannot be " +
                        "identified in this audit or cross-referenced against the graph");
                }
                else
                {
                    object idValue = idField.GetValue(gen);
                    if (idValue == null)
                    {
                        idText = "UNREADABLE ('id' read as null)";
                        unreadable.Add(
                            $"node #{nodeCount} of type '{t.FullName}' read its 'id' field as null");
                    }
                    else
                    {
                        idText = idValue.ToString();
                    }
                }

                // FullName as well as Name: MapMagic ships two live Levels200 classes -
                // MapMagic.Nodes.MatrixGenerators.Levels200 (MatrixModifiers.cs:101) and
                // MapMagic.Nodes.MatrixSetsGenerators.Levels200 (MatrixSetsGenerators.cs:49). A report that
                // prints only "Levels200" cannot tell a reader which one is in the graph, and the
                // `t.Name == "Levels200"` dispatch below treats them as the same node type.
                body.AppendLine($"Node ID: {idText} | Type: {t.Name} ({t.FullName})");

                if (t.Name == "HectonSandboxAbyssalShelfMapMagicNode")
                    absentFields += DumpRequestedFields(gen, t, idText, ShelfNodeFields, body, unreadable);
                else if (t.Name == "Levels200")
                    absentFields += DumpRequestedFields(gen, t, idText, LevelsFields, body, unreadable);
                else if (t.Name == "Blend200")
                    absentFields += DumpRequestedFields(gen, t, idText, BlendFields, body, unreadable);

                // Single-inlet nodes are their own inlet: Levels200 is
                // `Generator, IInlet<MatrixWorld>, IOutlet<MatrixWorld>` (MatrixModifiers.cs:101) and the
                // graph stores its incoming edge under the NODE as the dictionary key. The old code only
                // walked IMultiInlet.Inlets(), so every single-inlet node printed with no INLET line at
                // all and the "tree" it claimed to map was missing those edges entirely - silently.
                bool declaresMultiInlet = false;
                bool isSelfInlet = false;
                foreach (Type itf in t.GetInterfaces())
                {
                    if (itf.Name == "IMultiInlet") declaresMultiInlet = true;
                    if (itf.Name.StartsWith("IInlet", StringComparison.Ordinal)) isSelfInlet = true;
                }

                if (isSelfInlet)
                {
                    inletsInspected++;
                    if (ReportLink(graphObj, getLinkMethod, gen, "SELF (node is its own inlet)", idText,
                                   body, unreadable))
                    {
                        linksResolved++;
                    }
                }

                IEnumerable inlets = null;
                if (declaresMultiInlet)
                {
                    try
                    {
                        // Name+arity match, not GetMethod(name, flags): the latter throws on overloads and
                        // Invoke(gen, null) throws if the match takes parameters. Both used to vanish into
                        // an empty catch, leaving the node looking unconnected.
                        MethodInfo inletsMethod = null;
                        foreach (MethodInfo mi in t.GetMethods(AnyInstance))
                        {
                            if (mi.Name != "Inlets" &&
                                !mi.Name.EndsWith(".Inlets", StringComparison.Ordinal)) continue;
                            if (mi.GetParameters().Length != 0) continue;
                            inletsMethod = mi;
                            break;
                        }

                        if (inletsMethod != null)
                        {
                            inlets = inletsMethod.Invoke(gen, null) as IEnumerable;
                        }
                        else
                        {
                            PropertyInfo p = t.GetProperty("Inlets", AnyInstance);
                            if (p != null) inlets = p.GetValue(gen) as IEnumerable;
                        }

                        if (inlets == null)
                        {
                            body.AppendLine(
                                "  - INLETS UNREADABLE: type implements IMultiInlet but no parameterless " +
                                "Inlets() member returned an enumerable.");
                            unreadable.Add(
                                $"node {idText} ('{t.FullName}') implements IMultiInlet " +
                                "(Generator.cs:40-44) but its Inlets() could not be enumerated, so its " +
                                "incoming edges are missing from the link tree");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Was `catch {}`. A throw from Invoke made the node print zero INLET lines, which
                        // reads identically to "this node is unconnected" - the single most misleading
                        // thing a link-topology auditor can say.
                        body.AppendLine($"  - INLETS UNREADABLE: {ex.GetType().Name}: {ex.Message}");
                        unreadable.Add(
                            $"node {idText} ('{t.FullName}') threw while enumerating Inlets(): " +
                            $"{ex.GetType().Name}: {ex.Message}");
                        Debug.LogWarning(
                            $"[DynamicGraphAuditorTask] Node {idText} ('{t.FullName}') threw while " +
                            $"enumerating Inlets(); its incoming edges are absent from the audit: {ex}");
                    }
                }

                if (inlets != null)
                {
                    int inletIndex = -1;
                    foreach (object inlet in inlets)
                    {
                        inletIndex++;
                        if (inlet == null)
                        {
                            body.AppendLine($"  - INLET #{inletIndex}: NULL inlet object.");
                            unreadable.Add(
                                $"node {idText} ('{t.FullName}') yielded a null inlet at index " +
                                $"{inletIndex}");
                            continue;
                        }

                        inletsInspected++;
                        if (ReportLink(graphObj, getLinkMethod, inlet, DescribeInlet(inlet, inletIndex),
                                       idText, body, unreadable))
                        {
                            linksResolved++;
                        }
                    }
                }

                body.AppendLine("");
            }

            if (nodeCount == 0)
            {
                // A valid-but-empty generators array walked cleanly, appended nothing, and exited 0 with a
                // header-only report. Zero nodes is a finding, never a pass.
                throw new InvalidOperationException(
                    $"'{graphAssetPath}' enumerated ZERO generators. There is nothing to audit, so no " +
                    "report was written rather than publishing an empty document that reads as a clean " +
                    "graph.");
            }

            StringBuilder doc = new StringBuilder();
            doc.AppendLine("# MapMagic Graph Dynamic Audit");
            doc.AppendLine();
            doc.AppendLine($"Graph asset: {graphAssetPath}");
            doc.AppendLine($"Graph type: {graphType.FullName}");
            doc.AppendLine($"Nodes enumerated: {nodeCount}");
            doc.AppendLine($"Inlets inspected: {inletsInspected}");
            doc.AppendLine($"Inlet links resolved: {linksResolved}");
            doc.AppendLine($"Requested fields absent from their declaring type: {absentFields}");
            doc.AppendLine();

            if (unreadable.Count == 0)
            {
                doc.AppendLine(
                    "VERDICT: COMPLETE - every node was identified and every inlet's link state was read. " +
                    "Lines marked ABSENT below are fields this audit asked for that the type does not " +
                    "declare; they are a stale expectation in the auditor, not a missing value in the " +
                    "graph.");
            }
            else
            {
                doc.AppendLine($"VERDICT: INCOMPLETE - {unreadable.Count} fact(s) could not be read. Do");
                doc.AppendLine("NOT cite the link tree below as this graph's topology; edges are missing:");
                foreach (string u in unreadable) doc.AppendLine($"- {u}");
            }

            doc.AppendLine();
            doc.Append(body.ToString());

            File.WriteAllText(reportPath, doc.ToString(), Encoding.UTF8);

            // The headline numbers go into the Unity log too. The report file alone meant every finding
            // lived in a directory nobody reading the batchmode log would ever open.
            Debug.Log(
                $"[DynamicGraphAuditorTask] Wrote {reportPath}: {nodeCount} node(s), {inletsInspected} " +
                $"inlet(s) inspected, {linksResolved} link(s) resolved, {absentFields} absent field(s), " +
                $"{unreadable.Count} unreadable fact(s).");

            if (linksResolved == 0)
            {
                // Not fatal by itself - a graph really can be unwired, and that is a finding worth
                // publishing - but it must never slide past as an ordinary success line.
                Debug.LogWarning(
                    $"[DynamicGraphAuditorTask] ZERO inlet links resolved across {nodeCount} node(s) and " +
                    $"{inletsInspected} inlet(s). Either this graph is genuinely unwired or the link " +
                    $"lookup is reading the wrong member; check the per-node lines in {reportPath} before " +
                    "quoting this audit.");
            }

            if (unreadable.Count > 0)
            {
                throw new InvalidOperationException(
                    $"{unreadable.Count} fact(s) about '{graphAssetPath}' could not be read, so the link " +
                    $"tree in {reportPath} is not a complete topology. That report is stamped " +
                    "'VERDICT: INCOMPLETE' and must not be cited as a pass. First unreadable fact: " +
                    unreadable[0]);
            }
        }

        /// <summary>
        /// Prints the requested field values for one node. Returns how many of them the type does not
        /// declare. Previously `if (f != null)` dropped absent fields without a trace, so a reader could
        /// not tell an absent field from a field whose value happened not to be printed.
        /// </summary>
        private static int DumpRequestedFields(object gen, Type t, string idText, string[] fieldNames,
                                               StringBuilder body, List<string> unreadable)
        {
            int absent = 0;

            foreach (string fieldName in fieldNames)
            {
                FieldInfo f = t.GetField(fieldName, AnyInstance);
                if (f == null)
                {
                    absent++;
                    body.AppendLine(
                        $"  - {fieldName}: ABSENT - '{t.FullName}' declares no such field, so this audit " +
                        "reports no value for it (auditor expectation is stale, or the value lives " +
                        "elsewhere on the node).");
                    Debug.LogWarning(
                        $"[DynamicGraphAuditorTask] Node {idText} ('{t.FullName}') has no field " +
                        $"'{fieldName}'; the audit could not report its value.");
                    continue;
                }

                try
                {
                    object v = f.GetValue(gen);
                    string valueText = v != null ? v.ToString() : "null";
                    body.AppendLine($"  - {fieldName}: {valueText}");
                }
                catch (Exception ex)
                {
                    body.AppendLine($"  - {fieldName}: UNREADABLE - {ex.GetType().Name}: {ex.Message}");
                    unreadable.Add(
                        $"node {idText} ('{t.FullName}') threw reading field '{fieldName}': " +
                        $"{ex.GetType().Name}: {ex.Message}");
                }
            }

            return absent;
        }

        /// <summary>
        /// Resolves one inlet's incoming link and writes a line for it. Returns true only when an outlet
        /// was actually found. Every failure is written into the report AND recorded in
        /// <paramref name="unreadable"/>; the old version wrapped this whole block in `catch {}`, so a
        /// reflection failure and a genuinely unconnected inlet produced the same output: nothing.
        /// </summary>
        private static bool ReportLink(object graphObj, MethodInfo getLinkMethod, object inlet,
                                       string inletLabel, string idText, StringBuilder body,
                                       List<string> unreadable)
        {
            if (getLinkMethod == null)
            {
                body.AppendLine($"  - INLET {inletLabel} <- UNREADABLE: no GetLink method on the graph.");
                return false;
            }

            try
            {
                object outlet = getLinkMethod.Invoke(graphObj, new object[] { inlet });
                if (outlet == null)
                {
                    // Was printed as nothing at all, which is indistinguishable from a node whose inlets
                    // were never queried. An unconnected inlet is a real finding and has to be legible.
                    body.AppendLine($"  - INLET {inletLabel} <- UNCONNECTED (no link in the graph).");
                    return false;
                }

                Type outType = outlet.GetType();

                // Outlet<T>.Gen (Generator.cs:73) and Generator.Gen (Generator.cs:253, returns `this`) are
                // both properties, so GetProperty is the right lookup - but when it missed, the old code
                // substituted the literal string "Unknown" or, if the parent existed with no 'id' field,
                // printed "Outlet of Node " with an empty id and no complaint.
                PropertyInfo outGenProp = outType.GetProperty("Gen", AnyInstance);
                object parentNode = outGenProp != null ? outGenProp.GetValue(outlet) : null;

                if (parentNode == null)
                {
                    body.AppendLine(
                        $"  - INLET {inletLabel} <- LINKED to a {outType.FullName} whose source node is " +
                        "UNREADABLE (no 'Gen' property, or it read as null).");
                    unreadable.Add(
                        $"node {idText} inlet {inletLabel} is linked to a {outType.FullName} but the " +
                        "producing node could not be identified, so this edge has no source in the tree");
                    return false;
                }

                Type parentType = parentNode.GetType();
                FieldInfo parentIdField = parentType.GetField("id", AnyInstance);
                object parentId = parentIdField != null ? parentIdField.GetValue(parentNode) : null;

                if (parentId == null)
                {
                    body.AppendLine(
                        $"  - INLET {inletLabel} <- Outlet of a {parentType.FullName} with an UNREADABLE " +
                        "id.");
                    unreadable.Add(
                        $"node {idText} inlet {inletLabel} is fed by a {parentType.FullName} whose 'id' " +
                        "could not be read, so this edge cannot be cross-referenced");
                    return false;
                }

                body.AppendLine(
                    $"  - INLET {inletLabel} <- Outlet of Node {parentId} ({parentType.Name})");
                return true;
            }
            catch (Exception ex)
            {
                body.AppendLine($"  - INLET {inletLabel} <- UNREADABLE: {ex.GetType().Name}: {ex.Message}");
                unreadable.Add(
                    $"node {idText} inlet {inletLabel} threw during link lookup: {ex.GetType().Name}: " +
                    ex.Message);
                Debug.LogWarning(
                    $"[DynamicGraphAuditorTask] Link lookup threw for node {idText} inlet {inletLabel}; " +
                    $"that edge is absent from the audit: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Labels an inlet for the report. The old code read a "name" FIELD off the inlet, which
        /// MapMagic.Nodes.Inlet&lt;T&gt; does not have - Generator.cs:53-66 declares only gen/id/Gen/Id/
        /// LinkedOutletId/LinkedGenId. So every inlet printed as "unnamed", and a Blend200's layer inlets
        /// (MatrixModifiers.cs:258-274, one Inlet per layer) were mutually indistinguishable: the report
        /// could not say WHICH layer a link fed. Id plus the yield index fixes that; DumpNodeLinksTask.cs:29
        /// already used Id for the same reason.
        /// </summary>
        private static string DescribeInlet(object inlet, int inletIndex)
        {
            Type inType = inlet.GetType();

            string name = inType.GetField("name", AnyInstance)?.GetValue(inlet) as string;
            if (string.IsNullOrEmpty(name))
            {
                name = inType.GetProperty("name", AnyInstance)?.GetValue(inlet) as string;
            }

            object id = inType.GetField("id", AnyInstance)?.GetValue(inlet);
            string idPart = id != null ? id.ToString() : "no-id";

            return string.IsNullOrEmpty(name)
                ? $"#{inletIndex} (id {idPart}, {inType.Name})"
                : $"#{inletIndex} '{name}' (id {idPart}, {inType.Name})";
        }
    }
}
