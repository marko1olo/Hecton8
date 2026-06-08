#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Narrative.Editor
{
    /// <summary>
    /// In-memory Roslyn verification for the AppliedLore/Narrative runtime lane.
    /// Writes no reports; the C# source and Unity Console result are the proof artifact.
    /// </summary>
    public static class H8NarrativeApexVerifier
    {
        private const int MaxCallDepth = 8;
        private const int MaxFindingsLogged = 64;
        private const string TerminalOsScenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
        private const string TerminalOsScenePlacementPlanPath =
            "Docs/Lore/AppliedContent/binding_maps/RS001_RS010_scene_placement_plan.csv";
        private const string TerminalOsRuntimeObjectName = "__APPLIED_LORE_TERMINAL_OS_RUNTIME";
        private const string TerminalOsRuntimeScriptGuid = "0c18f0b5937ab1447ae790905fb2012b";
        private const string WorldSceneMapMagicMarker = "MapMagic::MapMagic.Core.MapMagicObject";
        private const string WorldSceneCrestMarker = "Crest";
        private const string AppliedLoreTerminalPreviewSignalName = "AppliedLoreTerminalPreviewSignal";
        private const string AudioGlitchParametersDtoName = "AudioGlitchParametersDTO";

        private static readonly string[] WorldSceneOceanPrefabPaths =
        {
            "Assets/_Project/Prefabs/Ocean_Crest.prefab",
            "Assets/_Project/Prefabs/Hecton Ocean.prefab"
        };

        private static readonly string[] RuntimeSourcePaths =
        {
            "Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs",
            "Assets/_Project/Scripts/Core/Signals/GlobalSignals.RuntimeLifecycle.cs",
            "Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs",
            "Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs",
            "Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs",
            "Assets/_Project/Scripts/Data/Monolith/H8AppliedLoreRuntime.cs",
            "Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs",
            "Assets/_Project/Scripts/Gameplay/MessageTerminal.cs",
            "Assets/_Project/Scripts/Gameplay/ScannableFragment.cs",
            "Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs",
            "Assets/_Project/Scripts/ScanEvents.cs",
            "Assets/_Project/Scripts/NarrativeDiscovery.cs",
            "Assets/_Project/Scripts/HectonNarrativeDirector.cs",
            "Assets/_Project/Scripts/Input/AccessibilitySettings.cs",
            "Assets/_Project/Scripts/Narrative/HectonNarrativeDirector_PoiTriggers.cs",
            "Assets/_Project/Scripts/Narrative/Campaign/MetaCampaignService.cs",
            "Assets/_Project/Scripts/Narrative/CorporateOrderSystem.cs",
            "Assets/_Project/Scripts/Narrative/LoreDatabaseManager.cs",
            "Assets/_Project/Scripts/Narrative/ProceduralLoreDirector.cs",
            "Assets/_Project/Scripts/Narrative/Prologue/AwaitableDropSequenceDirector.cs",
            "Assets/_Project/Scripts/NarrativeEvents.cs",
            "Assets/_Project/Scripts/UI/DiegeticHudManualLayout.cs",
            "Assets/_Project/Scripts/UI/FontStreamingManager.cs",
            "Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs",
            "Assets/_Project/Scripts/UI/SettingsManager.cs",
            "Assets/_Project/Scripts/UI/SettingsPanel.cs",
            "Assets/_Project/Scripts/UI/SubtitleManager.cs",
            "Assets/_Project/Scripts/UI/UIScreenShake.cs",
            "Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs",
            "Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime_TerminalProjection.cs"
        };

        private static readonly string[] SourceMetaRequiredExtensions =
        {
            ".asmdef",
            ".cs",
            ".shader",
            ".compute",
            ".prefab",
            ".asset",
            ".mat",
            ".unity",
            ".anim",
            ".controller",
            ".overrideController",
            ".playable",
            ".uxml",
            ".uss",
            ".png",
            ".jpg",
            ".jpeg",
            ".tga",
            ".psd",
            ".fbx",
            ".wav",
            ".ogg",
            ".mp3",
            ".bytes",
            ".csv",
            ".json",
            ".txt"
        };

        private static readonly string[] DependencyInvocationNames =
        {
            "GetComponent",
            "TryGetComponent",
            "GetComponents",
            "GetComponentInChildren",
            "GetComponentInParent",
            "GetComponentsInChildren",
            "GetComponentsInParent",
            "FindObjectOfType",
            "FindObjectsOfType",
            "FindFirstObjectByType",
            "FindAnyObjectByType",
            "FindObjectsByType",
            "Find",
            "FindWithTag"
        };

        private static readonly string[] PresentationInvocationNames =
        {
            "SetCharArray",
            "SetText",
            "SetPropertyBlock",
            "GetPropertyBlock",
            "SetActive",
            "PlayAtPoint",
            "PlayStatic2D",
            "PlayOneShot",
            "SetTexture",
            "SetBuffer",
            "SetFloat",
            "SetColor"
        };

        private static readonly string[] DataVaultWriteLockNames =
        {
            "TryAcquireWriteLock",
            "AcquireWriteLock",
            "BeginWriteLock",
            "TryWriteLock",
            "AcquireWrite",
            "TryAcquireWrite"
        };

        [MenuItem("Hecton8/Lore/Run Narrative Apex Verification")]
        public static void RunFromMenu()
        {
            ApexSummary summary = Run();
            string report = BuildConsoleReport(in summary);
            if (summary.FatalFindings == 0)
                Debug.Log(report);
            else
                Debug.LogError(report);
        }

        public static ApexSummary Run()
        {
            string projectRoot = ResolveProjectRoot();
            List<Finding> findings = new List<Finding>(64);
            List<FileUnit> files = new List<FileUnit>(RuntimeSourcePaths.Length);
            Dictionary<string, List<MethodDeclarationSyntax>> methodsByOwnerAndName =
                new Dictionary<string, List<MethodDeclarationSyntax>>(256, StringComparer.Ordinal);

            ApexSummary summary = default;
            summary.FilesExpected = RuntimeSourcePaths.Length;

            for (int i = 0; i < RuntimeSourcePaths.Length; i++)
                LoadFile(projectRoot, RuntimeSourcePaths[i], files, findings, ref summary);

            for (int i = 0; i < files.Count; i++)
                IndexMethods(files[i].Root, methodsByOwnerAndName, ref summary);

            ScanHotRoots(files, methodsByOwnerAndName, findings, ref summary);
            ScanAppliedLoreRouteBoundaries(projectRoot, files, findings, ref summary);
            ScanAppliedLoreSpatialUnlockBoundaries(projectRoot, findings, ref summary);
            ScanScannerLoreFragmentCompletionRoute(files, findings, ref summary);
            ScanScannableFragmentLifecycleRoute(files, findings, ref summary);
            ScanNarrativeDiscoveryHashCacheRoute(files, findings, ref summary);
            ScanHectonNarrativeDirectorPoiHashCacheRoute(files, findings, ref summary);
            ScanAppliedLoreWorldImpactPhaseRoute(files, findings, ref summary);
            ScanMetaCampaignPhaseSideEffectRoute(files, findings, ref summary);
            ScanPrologueBlackBoxDataVaultRoute(files, findings, ref summary);
            ScanPdaTelemetryVaultRoute(files, findings, ref summary);
            ScanTerminalOsTelemetryVaultRoute(files, findings, ref summary);
            ScanAppliedLoreTerminalPreviewRoute(files, findings, ref summary);
            ScanAccessibilityTextScaleProducerRoute(files, findings, ref summary);
            ScanAccessibilityMotionScaleRoute(files, findings, ref summary);
            ScanUiRescaleBroadcastRoute(files, findings, ref summary);
            ScanPdaCorruptedRecordRoute(files, findings, ref summary);
            ScanAudioLogGlitchRoute(projectRoot, files, findings, ref summary);
            ScanSubtitleAudioLogPhaseBridge(files, findings, ref summary);
            ScanTerminalOsBlackBoxDumpRoute(files, findings, ref summary);
            ScanLockPatterns(files, findings, ref summary);
            ScanTerminalOsSceneBinding(projectRoot, findings, ref summary);
            ScanRuntimeStructLayoutProof(files, findings, ref summary);
            ScanProjectAssetMetaIntegrity(projectRoot, findings, ref summary);

            summary.FatalFindings = findings.Count;
            summary.FindingsLogged = LogFindings(findings);
            return summary;
        }

        private static void LoadFile(
            string projectRoot,
            string relativePath,
            List<FileUnit> files,
            List<Finding> findings,
            ref ApexSummary summary)
        {
            string fullPath = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                findings.Add(new Finding(relativePath, 0, "missing_source", "Source file is absent"));
                summary.MissingFiles++;
                return;
            }

            string source;
            try
            {
                source = File.ReadAllText(fullPath, Encoding.UTF8);
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is NotSupportedException)
            {
                findings.Add(new Finding(relativePath, 0, "source_read", exception.GetType().Name));
                summary.ParseFailures++;
                return;
            }

            SyntaxTree tree = CSharpSyntaxTree.ParseText(source, path: fullPath);
            using (IEnumerator<Diagnostic> diagnostics = tree.GetDiagnostics().GetEnumerator())
            {
                while (diagnostics.MoveNext())
                {
                    Diagnostic diagnostic = diagnostics.Current;
                    if (diagnostic.Severity != DiagnosticSeverity.Error)
                        continue;

                    FileLinePositionSpan span = diagnostic.Location.GetLineSpan();
                    findings.Add(new Finding(relativePath, span.StartLinePosition.Line + 1, "syntax", diagnostic.Id));
                    summary.ParseFailures++;
                    return;
                }
            }

            files.Add(new FileUnit(relativePath, tree.GetCompilationUnitRoot()));
            summary.FilesParsed++;
        }

        private static bool TryLoadAuditFile(
            string projectRoot,
            string relativePath,
            List<Finding> findings,
            out FileUnit file)
        {
            file = default;
            string fullPath = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                findings.Add(new Finding(relativePath, 0, "missing_audit_source", "Source file is absent"));
                return false;
            }

            string source;
            try
            {
                source = File.ReadAllText(fullPath, Encoding.UTF8);
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is NotSupportedException)
            {
                findings.Add(new Finding(relativePath, 0, "audit_source_read", exception.GetType().Name));
                return false;
            }

            SyntaxTree tree = CSharpSyntaxTree.ParseText(source, path: fullPath);
            using (IEnumerator<Diagnostic> diagnostics = tree.GetDiagnostics().GetEnumerator())
            {
                while (diagnostics.MoveNext())
                {
                    Diagnostic diagnostic = diagnostics.Current;
                    if (diagnostic.Severity != DiagnosticSeverity.Error)
                        continue;

                    FileLinePositionSpan span = diagnostic.Location.GetLineSpan();
                    findings.Add(new Finding(relativePath, span.StartLinePosition.Line + 1, "audit_source_parse", diagnostic.Id));
                    return false;
                }
            }

            file = new FileUnit(relativePath, tree.GetCompilationUnitRoot());
            return true;
        }

        private static void IndexMethods(
            CompilationUnitSyntax root,
            Dictionary<string, List<MethodDeclarationSyntax>> methodsByOwnerAndName,
            ref ApexSummary summary)
        {
            using (IEnumerator<TypeDeclarationSyntax> types = root.DescendantNodes().OfType<TypeDeclarationSyntax>().GetEnumerator())
            {
                while (types.MoveNext())
                {
                    TypeDeclarationSyntax type = types.Current;
                    string owner = type.Identifier.ValueText;
                    using (IEnumerator<MethodDeclarationSyntax> methods = type.Members.OfType<MethodDeclarationSyntax>().GetEnumerator())
                    {
                        while (methods.MoveNext())
                        {
                            MethodDeclarationSyntax method = methods.Current;
                            string key = owner + "." + method.Identifier.ValueText;
                            if (!methodsByOwnerAndName.TryGetValue(key, out List<MethodDeclarationSyntax> list))
                            {
                                list = new List<MethodDeclarationSyntax>(2);
                                methodsByOwnerAndName.Add(key, list);
                            }

                            list.Add(method);
                            summary.MethodsIndexed++;
                        }
                    }
                }
            }
        }

        private static void ScanHotRoots(
            List<FileUnit> files,
            Dictionary<string, List<MethodDeclarationSyntax>> methodsByOwnerAndName,
            List<Finding> findings,
            ref ApexSummary summary)
        {
            for (int i = 0; i < files.Count; i++)
            {
                CompilationUnitSyntax root = files[i].Root;
                using (IEnumerator<TypeDeclarationSyntax> types = root.DescendantNodes().OfType<TypeDeclarationSyntax>().GetEnumerator())
                {
                    while (types.MoveNext())
                    {
                        TypeDeclarationSyntax type = types.Current;
                        string owner = type.Identifier.ValueText;
                        using (IEnumerator<MethodDeclarationSyntax> methods = type.Members.OfType<MethodDeclarationSyntax>().GetEnumerator())
                        {
                            while (methods.MoveNext())
                            {
                                MethodDeclarationSyntax method = methods.Current;
                                string methodName = method.Identifier.ValueText;
                                if (!IsApexRoot(methodName))
                                    continue;

                                summary.HotRootsScanned++;
                                bool presentationAllowed =
                                    string.Equals(methodName, "LateFrameTick", StringComparison.Ordinal) ||
                                    string.Equals(methodName, "VisualSyncTick", StringComparison.Ordinal);
                                ScanMethodRecursive(
                                    owner,
                                    method,
                                    methodName,
                                    presentationAllowed,
                                    methodsByOwnerAndName,
                                    findings,
                                    ref summary,
                                    new HashSet<string>(StringComparer.Ordinal),
                                    0);
                            }
                        }
                    }
                }
            }
        }

        private static void ScanMethodRecursive(
            string owner,
            MethodDeclarationSyntax method,
            string rootName,
            bool presentationAllowed,
            Dictionary<string, List<MethodDeclarationSyntax>> methodsByOwnerAndName,
            List<Finding> findings,
            ref ApexSummary summary,
            HashSet<string> stack,
            int depth)
        {
            if (depth > MaxCallDepth)
                return;

            string methodKey = BuildMethodInstanceKey(owner, method);
            if (!stack.Add(methodKey))
                return;

            summary.MethodsVisitedFromHotRoots++;
            ScanMethodBody(owner, method, rootName, presentationAllowed, findings, ref summary);

            if (method.Body != null)
            {
                using (IEnumerator<InvocationExpressionSyntax> invocations =
                    method.Body.DescendantNodes().OfType<InvocationExpressionSyntax>().GetEnumerator())
                {
                    while (invocations.MoveNext())
                    {
                        string calleeName = ResolveInvocationName(invocations.Current);
                        if (string.IsNullOrEmpty(calleeName))
                            continue;

                        if (!methodsByOwnerAndName.TryGetValue(owner + "." + calleeName, out List<MethodDeclarationSyntax> targets))
                            continue;

                        for (int i = 0; i < targets.Count; i++)
                            ScanMethodRecursive(owner, targets[i], rootName, presentationAllowed, methodsByOwnerAndName, findings, ref summary, stack, depth + 1);
                    }
                }
            }

            stack.Remove(methodKey);
        }

        private static void ScanMethodBody(
            string owner,
            MethodDeclarationSyntax method,
            string rootName,
            bool presentationAllowed,
            List<Finding> findings,
            ref ApexSummary summary)
        {
            SyntaxNode body = (SyntaxNode)method.Body ?? method.ExpressionBody;
            if (body == null)
                return;

            using (IEnumerator<SyntaxNode> nodes = body.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    SyntaxNode node = nodes.Current;
                    summary.SyntaxNodesScanned++;

                    if (node is MemberAccessExpressionSyntax access)
                    {
                        if (IsGlobalRegistryAccess(access))
                        {
                            findings.Add(new Finding(
                                RelativePath(node),
                                LineOf(node),
                                "hot_registry_access",
                                rootName + " -> " + owner + "." + method.Identifier.ValueText + " uses " + access.ToString()));
                            summary.DependencyFindings++;
                        }

                        continue;
                    }

                    if (node is InvocationExpressionSyntax invocation)
                    {
                        string invocationName = ResolveInvocationName(invocation);
                        string invocationText = invocation.ToString();

                        if (IsHotManagedAllocationInvocation(invocationName, invocationText))
                        {
                            findings.Add(new Finding(
                                RelativePath(node),
                                LineOf(node),
                                "hot_managed_allocation",
                                rootName + " -> " + owner + "." + method.Identifier.ValueText + " calls " + Trim(invocationText)));
                            summary.ZeroGcFindings++;
                            continue;
                        }

                        if (IsForbiddenDependencyInvocation(invocationName, invocationText))
                        {
                            findings.Add(new Finding(
                                RelativePath(node),
                                LineOf(node),
                                "hot_dependency_lookup",
                                rootName + " -> " + owner + "." + method.Identifier.ValueText + " calls " + Trim(invocationText)));
                            summary.DependencyFindings++;
                            continue;
                        }

                        if (!presentationAllowed && IsPresentationInvocation(invocationName, invocationText))
                        {
                            findings.Add(new Finding(
                                RelativePath(node),
                                LineOf(node),
                                "presentation_before_visual_sync",
                                rootName + " -> " + owner + "." + method.Identifier.ValueText + " calls " + Trim(invocationText)));
                            summary.PhaseFindings++;
                            continue;
                        }

                        if (!presentationAllowed && IsUnityEventInvoke(invocationText))
                        {
                            findings.Add(new Finding(
                                RelativePath(node),
                                LineOf(node),
                                "managed_event_before_visual_sync",
                                rootName + " -> " + owner + "." + method.Identifier.ValueText + " calls " + Trim(invocationText)));
                            summary.PhaseFindings++;
                        }

                        if (IsDirectJobHandleComplete(invocationName, invocationText))
                        {
                            findings.Add(new Finding(
                                RelativePath(node),
                                LineOf(node),
                                "hot_direct_job_complete",
                                rootName + " -> " + owner + "." + method.Identifier.ValueText + " calls " + Trim(invocationText)));
                            summary.JobCompleteFindings++;
                        }

                        continue;
                    }

                    if (node is BinaryExpressionSyntax binaryExpression &&
                        IsHotStringConcatenation(binaryExpression))
                    {
                        findings.Add(new Finding(
                            RelativePath(node),
                            LineOf(node),
                            "hot_string_concat",
                            rootName + " -> " + owner + "." + method.Identifier.ValueText + " concatenates " + Trim(binaryExpression.ToString())));
                        summary.ZeroGcFindings++;
                        continue;
                    }

                    if (IsHotManagedAllocationSyntax(node))
                    {
                        findings.Add(new Finding(
                            RelativePath(node),
                            LineOf(node),
                            "hot_managed_allocation",
                            rootName + " -> " + owner + "." + method.Identifier.ValueText + " allocates " + Trim(node.ToString())));
                        summary.ZeroGcFindings++;
                        continue;
                    }

                    if (!presentationAllowed &&
                        node is AssignmentExpressionSyntax assignment &&
                        IsTextAssignment(assignment))
                    {
                        findings.Add(new Finding(
                            RelativePath(node),
                            LineOf(node),
                            "presentation_before_visual_sync",
                            rootName + " -> " + owner + "." + method.Identifier.ValueText + " assigns " + Trim(assignment.ToString())));
                        summary.PhaseFindings++;
                    }
                }
            }
        }

        private static void ScanLockPatterns(List<FileUnit> files, List<Finding> findings, ref ApexSummary summary)
        {
            for (int i = 0; i < files.Count; i++)
            {
                using (IEnumerator<MethodDeclarationSyntax> methods =
                    files[i].Root.DescendantNodes().OfType<MethodDeclarationSyntax>().GetEnumerator())
                {
                    while (methods.MoveNext())
                    {
                        MethodDeclarationSyntax method = methods.Current;
                        SyntaxNode body = (SyntaxNode)method.Body ?? method.ExpressionBody;
                        if (body == null)
                            continue;

                        HashSet<TryStatementSyntax> checkedWriteLockTries = new HashSet<TryStatementSyntax>();
                        using (IEnumerator<InvocationExpressionSyntax> invocations =
                            body.DescendantNodes().OfType<InvocationExpressionSyntax>().GetEnumerator())
                        {
                            while (invocations.MoveNext())
                            {
                                InvocationExpressionSyntax invocation = invocations.Current;
                                string name = ResolveInvocationName(invocation);
                                string text = invocation.ToString();
                                if (IsDataVaultWriteLock(name, text))
                                {
                                    summary.DataVaultWriteLocksChecked++;
                                    bool directWriteLockSafe = HasDataVaultWriteReleaseFinally(invocation);
                                    if (!directWriteLockSafe && IsDataVaultWriteAcquireTransferHelper(method))
                                    {
                                        summary.DataVaultWriteLockHelpersChecked++;
                                        directWriteLockSafe = HasTransferHelperFailureReleaseFinally(method);
                                    }

                                    if (!directWriteLockSafe)
                                    {
                                        findings.Add(new Finding(
                                            RelativePath(invocation),
                                            LineOf(invocation),
                                            "data_vault_lock_without_release_finally",
                                            method.Identifier.ValueText + " calls " + Trim(text)));
                                        summary.LockFindings++;
                                    }

                                    TryStatementSyntax lockTry = invocation.FirstAncestorOrSelf<TryStatementSyntax>();
                                    if (lockTry != null && checkedWriteLockTries.Add(lockTry))
                                    {
                                        int locksInTry = CountDataVaultWriteLocks(lockTry.Block);
                                        if (locksInTry > 1)
                                        {
                                            findings.Add(new Finding(
                                                RelativePath(lockTry),
                                                LineOf(lockTry),
                                                "nested_data_vault_write_lock_risk",
                                                method.Identifier.ValueText + " has " + locksInTry + " write-lock acquires in one try/finally scope"));
                                            summary.LockFindings++;
                                        }
                                    }

                                    continue;
                                }

                                if (IsDataVaultWriteAcquireHelperCall(invocation, out string releaseInvocationName))
                                {
                                    summary.DataVaultWriteLockHelperCallersChecked++;
                                    if (!HasImmediateTryFinallyWithInvocationAfterCall(invocation, releaseInvocationName))
                                    {
                                        findings.Add(new Finding(
                                            RelativePath(invocation),
                                            LineOf(invocation),
                                            "data_vault_helper_write_without_caller_finally",
                                            method.Identifier.ValueText + " calls " + Trim(text)));
                                        summary.LockFindings++;
                                    }
                                }

                                if (string.Equals(name, "TryAcquireFrameSnapshotForOwnerWrite", StringComparison.Ordinal))
                                {
                                    summary.DataVaultOwnerWriteScopesChecked++;
                                    if (!HasImmediateTryFinallyWithInvocationAfterCall(invocation, "ReleaseFrameSnapshotOwnerWrite"))
                                    {
                                        findings.Add(new Finding(
                                            RelativePath(invocation),
                                            LineOf(invocation),
                                            "data_vault_owner_write_without_caller_finally",
                                            method.Identifier.ValueText + " calls " + Trim(text)));
                                        summary.LockFindings++;
                                    }
                                }

                                if (string.Equals(name, "LockBufferForWrite", StringComparison.Ordinal))
                                {
                                    summary.GpuWriteLocksChecked++;
                                    if (!HasImmediateTryFinallyWithInvocationAfterCall(invocation, "UnlockBufferAfterWrite"))
                                    {
                                        findings.Add(new Finding(
                                            RelativePath(invocation),
                                            LineOf(invocation),
                                            "gpu_lock_without_unlock_finally",
                                            method.Identifier.ValueText + " calls " + Trim(text)));
                                        summary.LockFindings++;
                                    }
                                    else
                                    {
                                        summary.GpuWriteUnlockFinallyChecked++;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void ScanAppliedLoreRouteBoundaries(
            string projectRoot,
            List<FileUnit> files,
            List<Finding> findings,
            ref ApexSummary summary)
        {
            for (int i = 0; i < files.Count; i++)
            {
                FileUnit file = files[i];
                if (IsAllowedAppliedLoreArenaOwner(file.RelativePath))
                    continue;

                using (IEnumerator<InvocationExpressionSyntax> invocations =
                    file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>().GetEnumerator())
                {
                    while (invocations.MoveNext())
                    {
                        InvocationExpressionSyntax invocation = invocations.Current;
                        if (!IsDirectAppliedLoreArenaInvocation(invocation))
                            continue;

                        findings.Add(new Finding(
                            RelativePath(invocation),
                            LineOf(invocation),
                            "applied_lore_route_bypass",
                            "Use H8AppliedLoreRuntime instead of " + Trim(invocation.ToString())));
                        summary.DependencyFindings++;
                    }
                }
            }

            ScanProjectAppliedLoreBoundaryCandidates(projectRoot, files, findings, ref summary);
        }

        private static void ScanProjectAppliedLoreBoundaryCandidates(
            string projectRoot,
            List<FileUnit> alreadyParsedFiles,
            List<Finding> findings,
            ref ApexSummary summary)
        {
            string scriptsRoot = Path.Combine(
                projectRoot,
                "Assets",
                "_Project",
                "Scripts");

            if (!Directory.Exists(scriptsRoot))
            {
                findings.Add(new Finding(
                    "Assets/_Project/Scripts",
                    0,
                    "missing_scripts_root",
                    "Cannot scan AppliedLore route boundaries"));
                summary.DependencyFindings++;
                return;
            }

            string[] sourcePaths;
            try
            {
                sourcePaths = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is NotSupportedException)
            {
                findings.Add(new Finding(
                    "Assets/_Project/Scripts",
                    0,
                    "scripts_scan_failed",
                    exception.GetType().Name));
                summary.DependencyFindings++;
                return;
            }

            summary.AppliedLoreBoundaryFilesScanned = sourcePaths.Length;
            for (int i = 0; i < sourcePaths.Length; i++)
            {
                string relativePath = ToProjectRelativePath(projectRoot, sourcePaths[i]);
                if (IsAllowedAppliedLoreArenaOwner(relativePath) ||
                    IsAlreadyParsedRuntimeSource(relativePath, alreadyParsedFiles))
                {
                    continue;
                }

                string source;
                try
                {
                    source = File.ReadAllText(sourcePaths[i], Encoding.UTF8);
                }
                catch (Exception exception) when (
                    exception is IOException ||
                    exception is UnauthorizedAccessException ||
                    exception is ArgumentException ||
                    exception is NotSupportedException)
                {
                    findings.Add(new Finding(
                        relativePath,
                        0,
                        "source_read",
                        exception.GetType().Name));
                    summary.DependencyFindings++;
                    continue;
                }

                if (source.IndexOf("H8StaticDataArena.", StringComparison.Ordinal) < 0)
                    continue;

                summary.AppliedLoreBoundaryCandidateFilesParsed++;
                SyntaxTree tree = CSharpSyntaxTree.ParseText(source, path: sourcePaths[i]);
                CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
                using (IEnumerator<InvocationExpressionSyntax> invocations =
                    root.DescendantNodes().OfType<InvocationExpressionSyntax>().GetEnumerator())
                {
                    while (invocations.MoveNext())
                    {
                        InvocationExpressionSyntax invocation = invocations.Current;
                        if (!IsDirectAppliedLoreArenaInvocation(invocation))
                            continue;

                        findings.Add(new Finding(
                            RelativePath(invocation),
                            LineOf(invocation),
                            "applied_lore_route_bypass",
                            "Use H8AppliedLoreRuntime instead of " + Trim(invocation.ToString())));
                        summary.DependencyFindings++;
                    }
                }
            }
        }

        private static void ScanAppliedLoreSpatialUnlockBoundaries(
            string projectRoot,
            List<Finding> findings,
            ref ApexSummary summary)
        {
            string scriptsRoot = Path.Combine(projectRoot, "Assets", "_Project", "Scripts");
            if (!Directory.Exists(scriptsRoot))
                return;

            string[] sourcePaths;
            try
            {
                sourcePaths = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is NotSupportedException)
            {
                findings.Add(new Finding(
                    "Assets/_Project/Scripts",
                    0,
                    "scripts_scan_failed",
                    exception.GetType().Name));
                summary.DependencyFindings++;
                return;
            }

            summary.AppliedLoreUnlockFilesScanned = sourcePaths.Length;
            for (int i = 0; i < sourcePaths.Length; i++)
            {
                string relativePath = ToProjectRelativePath(projectRoot, sourcePaths[i]);
                if (relativePath.EndsWith("H8AppliedLoreRuntime.cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                string source;
                try
                {
                    source = File.ReadAllText(sourcePaths[i], Encoding.UTF8);
                }
                catch (Exception exception) when (
                    exception is IOException ||
                    exception is UnauthorizedAccessException ||
                    exception is ArgumentException ||
                    exception is NotSupportedException)
                {
                    findings.Add(new Finding(
                        relativePath,
                        0,
                        "source_read",
                        exception.GetType().Name));
                    summary.DependencyFindings++;
                    continue;
                }

                bool hasHashOnlyUnlockCandidate = source.IndexOf("TryRaisePacketUnlocked(", StringComparison.Ordinal) >= 0;
                bool hasDirectLoreSignalCandidate = source.IndexOf("SignalBus<LoreFragmentScannedSignal>", StringComparison.Ordinal) >= 0;
                if (!hasHashOnlyUnlockCandidate && !hasDirectLoreSignalCandidate)
                    continue;

                summary.AppliedLoreUnlockCandidateFilesParsed++;
                SyntaxTree tree = CSharpSyntaxTree.ParseText(source, path: sourcePaths[i]);
                CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
                using (IEnumerator<InvocationExpressionSyntax> invocations =
                    root.DescendantNodes().OfType<InvocationExpressionSyntax>().GetEnumerator())
                {
                    while (invocations.MoveNext())
                    {
                        InvocationExpressionSyntax invocation = invocations.Current;
                        if (!IsDirectAppliedLoreHashOnlyUnlock(invocation))
                        {
                            if (!IsDirectLoreFragmentSignalPublish(invocation))
                                continue;

                            if (IsAllowedScannerLoreFragmentSignalPublish(invocation))
                            {
                                summary.ScannerLoreFragmentAllowedDirectPublishes++;
                                continue;
                            }

                            findings.Add(new Finding(
                                RelativePath(invocation),
                                LineOf(invocation),
                                "applied_lore_signal_bypass",
                                "Publish lore fragment commits through H8AppliedLoreRuntime.TryRaisePacketUnlockedAt"));
                            summary.DependencyFindings++;
                            continue;
                        }

                        findings.Add(new Finding(
                            RelativePath(invocation),
                            LineOf(invocation),
                            "applied_lore_non_spatial_unlock",
                            "Use H8AppliedLoreRuntime.TryRaisePacketUnlockedAt when caller has or can resolve AUP"));
                        summary.DependencyFindings++;
                    }
                }
            }
        }

        private static void ScanScannerLoreFragmentCompletionRoute(
            List<FileUnit> files,
            List<Finding> findings,
            ref ApexSummary summary)
        {
            if (!TryFindFile(files, "ScannerDataMiningRouter.cs", out FileUnit scannerFile) ||
                !TryFindFile(files, "PDAEncyclopediaStreamer.cs", out FileUnit pdaFile) ||
                !TryFindFile(files, "GlobalSignalPayloads.DomainRemainder.cs", out FileUnit signalFile) ||
                !TryFindFile(files, "GlobalSignals.RuntimeLifecycle.cs", out FileUnit signalLifecycleFile) ||
                !TryFindFile(files, "H8AppliedLoreRuntime.cs", out FileUnit appliedLoreRuntimeFile) ||
                !TryFindFile(files, "ScanEvents.cs", out FileUnit scanEventsFile))
            {
                findings.Add(new Finding(
                    "ScannerLoreFragmentCompletion",
                    0,
                    "scanner_lore_fragment_sources_missing",
                    "scanner/pda/signals/runtime/applied-lore/scan-events route source missing"));
                summary.DependencyFindings++;
                return;
            }

            summary.ScannerLoreFragmentSignalLayout =
                CountTextInStruct(signalFile.Root, "LoreFragmentScannedSignal", "public struct LoreFragmentScannedSignal : ISignal") +
                CountTextInStruct(signalFile.Root, "LoreFragmentScannedSignal", "StructLayout(LayoutKind.Explicit, Size = 64)") +
                CountTextInStruct(signalFile.Root, "LoreFragmentScannedSignal", "public const byte FlagPairedScanComplete = 1 << 0") +
                CountTextInStruct(signalFile.Root, "LoreFragmentScannedSignal", "public const byte FlagHasAup = 1 << 1") +
                CountTextInStruct(signalFile.Root, "LoreFragmentScannedSignal", "[FieldOffset(0)] public AbsoluteUniversePosition PositionAup") +
                CountTextInStruct(signalFile.Root, "LoreFragmentScannedSignal", "[FieldOffset(48)] public uint Hash") +
                CountTextInStruct(signalFile.Root, "LoreFragmentScannedSignal", "[FieldOffset(52)] public uint Frame") +
                CountTextInStruct(signalFile.Root, "LoreFragmentScannedSignal", "[FieldOffset(56)] public uint SourceId") +
                CountTextInStruct(signalFile.Root, "LoreFragmentScannedSignal", "[FieldOffset(60)] public byte Flags") +
                CountTextInStruct(signalFile.Root, "LoreFragmentScannedSignal", "public const int SizeBytes = 64") +
                CountTextInFile(signalLifecycleFile, "ValidateSignalSize<LoreFragmentScannedSignal>(LoreFragmentScannedSignal.SizeBytes)");
            summary.ScannerLoreFragmentCompletionPublishes =
                CountTextInMethod(scannerFile.Root, "RouteCompletionIfNeeded", "SignalBus<LoreFragmentScannedSignal>.TryPushTracked");
            summary.ScannerLoreFragmentCompletionFields =
                CountTextInMethod(scannerFile.Root, "RouteCompletionIfNeeded", "PositionAup = aup") +
                CountTextInMethod(scannerFile.Root, "RouteCompletionIfNeeded", "Hash = result.EntityHash") +
                CountTextInMethod(scannerFile.Root, "RouteCompletionIfNeeded", "Frame = frame") +
                CountTextInMethod(scannerFile.Root, "RouteCompletionIfNeeded", "SourceId = ScannerToolHash") +
                CountTextInMethod(scannerFile.Root, "RouteCompletionIfNeeded", "LoreFragmentScannedSignal.FlagHasAup");
            summary.ScannerLoreFragmentPairedScanComplete =
                CountTextInMethod(scannerFile.Root, "RouteCompletionIfNeeded", "SignalBus<ScanCompleteSignal>.TryPushTracked") +
                CountTextInMethod(scannerFile.Root, "RouteCompletionIfNeeded", "LoreFragmentScannedSignal.FlagPairedScanComplete");
            summary.ScannerLoreFragmentPdaSnapshotReads =
                CountInvocationTextInMethod(pdaFile.Root, "ConsumeScanSignals", "SignalBus<LoreFragmentScannedSignal>.GetFrameSnapshot");
            summary.ScannerLoreFragmentPdaAupReads =
                CountTextInMethod(pdaFile.Root, "ConsumeScanSignals", "TryCaptureSignalAup(in signal") +
                CountTextInMethod(pdaFile.Root, "ConsumeScanSignals", "hasSignalAup = true") +
                CountTextInMethod(pdaFile.Root, "TryCaptureSignalAup", "UnsafeUtility.As<LoreFragmentScannedSignal, PdaAup48>");
            summary.ScannerLoreFragmentPdaScanCompleteAupFiniteChecks =
                CountTextInMethod(pdaFile.Root, "ConsumeScanSignals", "TryCaptureSignalAup(in signal, out PdaAup48 signalAup)") +
                CountTextInMethod(pdaFile.Root, "ConsumeScanSignals", "hasSignalAup, validatePayload: false") +
                CountTextInMethod(pdaFile.Root, "TryCaptureSignalAup", "UnsafeUtility.As<ScanCompleteSignal, PdaAup48>");
            summary.ScannerLoreFragmentPdaUnlockCalls =
                CountTextInMethod(pdaFile.Root, "ConsumeScanSignals", "UnlockEntry(signal.Hash, in aup, signal.SourceId, signal.Frame, hasSignalAup)");
            summary.ScannerLoreFragmentPdaPairedDedupes =
                CountTextInMethod(pdaFile.Root, "ConsumeScanSignals", "LoreFragmentScannedSignal.FlagPairedScanComplete") +
                CountTextInMethod(pdaFile.Root, "ConsumeScanSignals", "HasPairedScanComplete(scanSignals, in signal)") +
                CountTextInMethod(pdaFile.Root, "HasPairedScanComplete", "scan.EntryHash == loreSignal.Hash") +
                CountTextInMethod(pdaFile.Root, "HasPairedScanComplete", "scan.SourceId == loreSignal.SourceId");
            summary.ScannerLoreFragmentAppliedLoreAupPublishes =
                CountTextInMethod(appliedLoreRuntimeFile.Root, "TryRaisePacketUnlockedAt", "LoreFragmentScannedSignal.FlagHasAup") +
                CountTextInMethod(appliedLoreRuntimeFile.Root, "TryRaisePacketUnlockedCore", "PositionAup = positionAup");
            summary.ScannerLoreFragmentAppliedLorePairedFlags =
                CountTextInMethod(appliedLoreRuntimeFile.Root, "TryRaisePacketUnlockedAt", "LoreFragmentScannedSignal.FlagPairedScanComplete");
            summary.ScannerLoreFragmentHashOnlyFlagStrips =
                CountTextInMethod(appliedLoreRuntimeFile.Root, "TryRaisePacketUnlocked", "flags & ~(LoreFragmentScannedSignal.FlagHasAup | LoreFragmentScannedSignal.FlagPairedScanComplete)") +
                CountTextInMethod(appliedLoreRuntimeFile.Root, "TryRaisePacketUnlockedAt", "flags & ~(LoreFragmentScannedSignal.FlagHasAup | LoreFragmentScannedSignal.FlagPairedScanComplete)");
            summary.ScannerLoreFragmentScanEventsColdPrewarm =
                CountTextInFile(scanEventsFile, "public static void EnsureInitializedCold()") +
                CountInvocationInMethod(scannerFile.Root, "OnEnable", "ScanEvents.EnsureInitializedCold");
            summary.ScannerLoreFragmentLegacyDirectDequeues =
                CountTextInFilesExcept(files, "TryDequeueLoreFragmentScanned", "GlobalSignals.LegacyFacade.cs");

            bool valid =
                summary.ScannerLoreFragmentSignalLayout >= 10 &&
                summary.ScannerLoreFragmentCompletionPublishes == 1 &&
                summary.ScannerLoreFragmentCompletionFields >= 5 &&
                summary.ScannerLoreFragmentPairedScanComplete >= 2 &&
                summary.ScannerLoreFragmentPdaSnapshotReads == 1 &&
                summary.ScannerLoreFragmentPdaAupReads >= 2 &&
                summary.ScannerLoreFragmentPdaScanCompleteAupFiniteChecks >= 3 &&
                summary.ScannerLoreFragmentPdaUnlockCalls == 1 &&
                summary.ScannerLoreFragmentPdaPairedDedupes >= 4 &&
                summary.ScannerLoreFragmentAppliedLoreAupPublishes >= 2 &&
                summary.ScannerLoreFragmentAppliedLorePairedFlags >= 1 &&
                summary.ScannerLoreFragmentHashOnlyFlagStrips >= 2 &&
                summary.ScannerLoreFragmentScanEventsColdPrewarm >= 2 &&
                summary.ScannerLoreFragmentLegacyDirectDequeues == 0;

            if (valid)
                return;

            findings.Add(new Finding(
                "ScannerLoreFragmentCompletion",
                0,
                "scanner_lore_fragment_route_incomplete",
                "layout=" + summary.ScannerLoreFragmentSignalLayout +
                " publishes=" + summary.ScannerLoreFragmentCompletionPublishes +
                " fields=" + summary.ScannerLoreFragmentCompletionFields +
                " paired=" + summary.ScannerLoreFragmentPairedScanComplete +
                " pda_snapshot=" + summary.ScannerLoreFragmentPdaSnapshotReads +
                " pda_aup=" + summary.ScannerLoreFragmentPdaAupReads +
                " pda_scan_complete_aup_checks=" + summary.ScannerLoreFragmentPdaScanCompleteAupFiniteChecks +
                " pda_unlocks=" + summary.ScannerLoreFragmentPdaUnlockCalls +
                " pda_paired_dedupes=" + summary.ScannerLoreFragmentPdaPairedDedupes +
                " applied_aup=" + summary.ScannerLoreFragmentAppliedLoreAupPublishes +
                " applied_paired_flags=" + summary.ScannerLoreFragmentAppliedLorePairedFlags +
                " flag_strips=" + summary.ScannerLoreFragmentHashOnlyFlagStrips +
                " scan_events_cold_prewarm=" + summary.ScannerLoreFragmentScanEventsColdPrewarm +
                " legacy_dequeues=" + summary.ScannerLoreFragmentLegacyDirectDequeues));
            summary.DependencyFindings++;
        }

        private static void ScanAppliedLoreTerminalPreviewRoute(
            List<FileUnit> files,
            List<Finding> findings,
            ref ApexSummary summary)
        {
            FileUnit signalFile;
            FileUnit signalLifecycleFile;
            FileUnit signalBusFile;
            FileUnit appliedLoreRuntimeFile;
            FileUnit staticDataArenaFile;
            FileUnit messageTerminalFile;
            FileUnit terminalOsFile;
            bool hasSignalFile = TryFindFile(files, "GlobalSignalPayloads.DomainRemainder.cs", out signalFile);
            bool hasSignalLifecycleFile = TryFindFile(files, "GlobalSignals.RuntimeLifecycle.cs", out signalLifecycleFile);
            bool hasSignalBusFile = TryFindFile(files, "SignalBusRuntime.cs", out signalBusFile);
            bool hasAppliedLoreRuntimeFile = TryFindFile(files, "H8AppliedLoreRuntime.cs", out appliedLoreRuntimeFile);
            bool hasStaticDataArenaFile = TryFindFile(files, "H8StaticDataArena.cs", out staticDataArenaFile);
            bool hasMessageTerminalFile = TryFindFile(files, "MessageTerminal.cs", out messageTerminalFile);
            bool hasTerminalOsFile = TryFindFile(files, "TerminalOsRuntime.cs", out terminalOsFile);

            if (!hasSignalFile ||
                !hasSignalLifecycleFile ||
                !hasSignalBusFile ||
                !hasAppliedLoreRuntimeFile ||
                !hasStaticDataArenaFile ||
                !hasMessageTerminalFile ||
                !hasTerminalOsFile)
            {
                findings.Add(new Finding(
                    "AppliedLoreTerminalPreview",
                    0,
                    "terminal_preview_route_sources_missing",
                    "signal=" + hasSignalFile +
                    " lifecycle=" + hasSignalLifecycleFile +
                    " bus=" + hasSignalBusFile +
                    " runtime=" + hasAppliedLoreRuntimeFile +
                    " arena=" + hasStaticDataArenaFile +
                    " publisher=" + hasMessageTerminalFile +
                    " consumer=" + hasTerminalOsFile));
                summary.DependencyFindings++;
                return;
            }

            string signalSource = signalFile.Root.ToFullString();
            bool hasFixedSignalDefinition =
                signalSource.IndexOf("public struct " + AppliedLoreTerminalPreviewSignalName + " : ISignal", StringComparison.Ordinal) >= 0 &&
                signalSource.IndexOf("StructLayout(LayoutKind.Explicit, Size = 32)", StringComparison.Ordinal) >= 0 &&
                signalSource.IndexOf("public const int SizeBytes = 32", StringComparison.Ordinal) >= 0 &&
                signalSource.IndexOf("LowTierFrameSignals = 8", StringComparison.Ordinal) >= 0 &&
                signalSource.IndexOf("LaneHash = 0x41545056u", StringComparison.Ordinal) >= 0;
            summary.TerminalPreviewSignalDefinitions = hasFixedSignalDefinition ? 1 : 0;
            if (!hasFixedSignalDefinition)
            {
                findings.Add(new Finding(
                    signalFile.RelativePath,
                    0,
                    "terminal_preview_signal_contract",
                    "AppliedLore terminal preview signal is not a fixed 32-byte lane contract"));
                summary.DependencyFindings++;
            }

            summary.TerminalPreviewSignalLifecycleSizeProofs = CountTextInFile(
                signalLifecycleFile,
                "ValidateSignalSize<" + AppliedLoreTerminalPreviewSignalName + ">(" + AppliedLoreTerminalPreviewSignalName + ".SizeBytes)");
            summary.AppliedLoreRuntimeLayoutProofs =
                CountTextInFile(appliedLoreRuntimeFile, "UnsafeUtility.SizeOf<H8AppliedLorePacketRecord>()") +
                CountTextInFile(appliedLoreRuntimeFile, "UnsafeUtility.SizeOf<H8AppliedLoreRouteRecord>()") +
                CountTextInFile(appliedLoreRuntimeFile, "UnsafeUtility.SizeOf<H8AppliedLoreWorldImpactRecord>()") +
                CountTextInFile(appliedLoreRuntimeFile, "UnsafeUtility.SizeOf<LoreFragmentScannedSignal>()") +
                CountTextInFile(appliedLoreRuntimeFile, "UnsafeUtility.SizeOf<" + AppliedLoreTerminalPreviewSignalName + ">()");
            if (summary.TerminalPreviewSignalLifecycleSizeProofs != 1 ||
                summary.AppliedLoreRuntimeLayoutProofs < 5)
            {
                findings.Add(new Finding(
                    appliedLoreRuntimeFile.RelativePath,
                    0,
                    "applied_lore_runtime_layout_proof",
                    "AppliedLore runtime must validate packet/route/world-impact/lore-signal/terminal-preview blittable sizes, lifecycle_size_proofs=" +
                    summary.TerminalPreviewSignalLifecycleSizeProofs +
                    " runtime_sizeof_proofs=" +
                    summary.AppliedLoreRuntimeLayoutProofs));
                summary.DependencyFindings++;
            }

            summary.AppliedLoreBootLayoutGuards = CountTextInFile(
                staticDataArenaFile,
                "!H8AppliedLoreRuntime.ValidateRuntimeLayout()");
            if (summary.AppliedLoreBootLayoutGuards != 1)
            {
                findings.Add(new Finding(
                    staticDataArenaFile.RelativePath,
                    0,
                    "applied_lore_boot_layout_guard",
                    "Resident Data Monolith validation must include H8AppliedLoreRuntime.ValidateRuntimeLayout, guards=" +
                    summary.AppliedLoreBootLayoutGuards));
                summary.DependencyFindings++;
            }

            string signalBusSource = signalBusFile.Root.ToFullString();
            summary.TerminalPreviewSignalBusContracts =
                signalBusSource.IndexOf("type == typeof(" + AppliedLoreTerminalPreviewSignalName + ")", StringComparison.Ordinal) >= 0 &&
                signalBusSource.IndexOf(AppliedLoreTerminalPreviewSignalName + ".ExpectedCapacity", StringComparison.Ordinal) >= 0 &&
                signalBusSource.IndexOf(AppliedLoreTerminalPreviewSignalName + ".LowTierFrameSignals", StringComparison.Ordinal) >= 0
                    ? 1
                    : 0;
            if (summary.TerminalPreviewSignalBusContracts != 1)
            {
                findings.Add(new Finding(
                    signalBusFile.RelativePath,
                    0,
                    "terminal_preview_signalbus_contract",
                    "SignalBus tuning table does not expose AppliedLore terminal preview capacity/low-tier lane"));
                summary.DependencyFindings++;
            }

            summary.TerminalPreviewPublisherCalls = CountInvocationTextInMethod(
                messageTerminalFile.Root,
                "PublishAppliedLoreTerminalPreview",
                "SignalBus<" + AppliedLoreTerminalPreviewSignalName + ">.TryPushTracked");
            if (summary.TerminalPreviewPublisherCalls != 1)
            {
                findings.Add(new Finding(
                    messageTerminalFile.RelativePath,
                    0,
                    "terminal_preview_publish_route",
                    "expected one MessageTerminal preview signal publish, actual=" + summary.TerminalPreviewPublisherCalls));
                summary.DependencyFindings++;
            }

            summary.TerminalPreviewSnapshotReads = CountInvocationTextInMethod(
                terminalOsFile.Root,
                "ConsumeAppliedLoreTerminalPreviewSignals",
                "SignalBus<" + AppliedLoreTerminalPreviewSignalName + ">.GetFrameSnapshot");
            if (summary.TerminalPreviewSnapshotReads != 1)
            {
                findings.Add(new Finding(
                    terminalOsFile.RelativePath,
                    0,
                    "terminal_preview_snapshot_route",
                    "expected one TerminalOS frame snapshot read, actual=" + summary.TerminalPreviewSnapshotReads));
                summary.PhaseFindings++;
            }

            summary.TerminalPreviewLateFrameCalls = CountInvocationInMethod(
                terminalOsFile.Root,
                "LateFrameTick",
                "ConsumeAppliedLoreTerminalPreviewSignals");
            if (summary.TerminalPreviewLateFrameCalls != 1)
            {
                findings.Add(new Finding(
                    terminalOsFile.RelativePath,
                    0,
                    "terminal_preview_phase_route",
                    "TerminalOS preview consumption must be owned by LateFrameTick, calls=" + summary.TerminalPreviewLateFrameCalls));
                summary.PhaseFindings++;
            }

            int destructiveConsumes = CountTextInFiles(
                files,
                "SignalBus<" + AppliedLoreTerminalPreviewSignalName + ">.TryConsumeFrame");
            if (destructiveConsumes != 0)
            {
                findings.Add(new Finding(
                    "AppliedLoreTerminalPreview",
                    0,
                    "terminal_preview_destructive_consume",
                    "preview lane must use snapshot reads, TryConsumeFrame calls=" + destructiveConsumes));
                summary.PhaseFindings++;
            }

            summary.TerminalPreviewPublicWriterDefinitions =
                CountTextInFile(terminalOsFile, "public bool ApplyTerminalAppliedLoreLine(") +
                CountTextInFile(terminalOsFile, "public bool TrySetTerminalAppliedLoreLine(");
            if (summary.TerminalPreviewPublicWriterDefinitions != 0)
            {
                findings.Add(new Finding(
                    terminalOsFile.RelativePath,
                    0,
                    "terminal_preview_public_writer",
                    "TerminalOS preview writer must remain private, public writers=" +
                    summary.TerminalPreviewPublicWriterDefinitions));
                summary.PhaseFindings++;
            }

            summary.TerminalPreviewExternalWriterCalls = CountTextInFilesExcept(
                files,
                "ApplyTerminalAppliedLoreLine(",
                "TerminalOsRuntime.cs");
            if (summary.TerminalPreviewExternalWriterCalls != 0)
            {
                findings.Add(new Finding(
                    "AppliedLoreTerminalPreview",
                    0,
                    "terminal_preview_external_writer",
                    "TerminalOS preview writes must enter through the fixed signal lane, external direct calls=" +
                    summary.TerminalPreviewExternalWriterCalls));
                summary.PhaseFindings++;
            }

            summary.TerminalOsGraphicsRebuildLateFrameCalls = CountInvocationInMethod(
                terminalOsFile.Root,
                "LateFrameTick",
                "FlushPendingGraphicsResourceRebuild");
            summary.TerminalOsGraphicsRebuildSlowTickCalls = CountInvocationInMethod(
                terminalOsFile.Root,
                "SlowTick",
                "FlushPendingGraphicsResourceRebuild");
            summary.TerminalOsGraphicsRebuildJobGuards =
                CountTextInMethod(terminalOsFile.Root, "FlushPendingGraphicsResourceRebuild", "_formatScheduled") +
                CountTextInMethod(terminalOsFile.Root, "FlushPendingGraphicsResourceRebuild", "_clickResolveScheduled") +
                CountTextInMethod(terminalOsFile.Root, "FlushPendingGraphicsResourceRebuild", "_terminalInteractionScheduled") +
                CountTextInMethod(terminalOsFile.Root, "FlushPendingGraphicsResourceRebuild", "_decryptionScheduled") +
                CountTextInMethod(terminalOsFile.Root, "FlushPendingGraphicsResourceRebuild", "return _graphicsResourcesReady");
            if (summary.TerminalOsGraphicsRebuildLateFrameCalls != 1 ||
                summary.TerminalOsGraphicsRebuildSlowTickCalls != 0 ||
                summary.TerminalOsGraphicsRebuildJobGuards < 5)
            {
                findings.Add(new Finding(
                    terminalOsFile.RelativePath,
                    0,
                    "terminal_os_graphics_rebuild_phase",
                    "TerminalOS graphics rebuild must flush in LateFrameTick only and wait for scheduled jobs, lateframe_calls=" +
                    summary.TerminalOsGraphicsRebuildLateFrameCalls +
                    " slowtick_calls=" +
                    summary.TerminalOsGraphicsRebuildSlowTickCalls +
                    " job_guards=" +
                    summary.TerminalOsGraphicsRebuildJobGuards));
                summary.PhaseFindings++;
            }

            summary.TerminalOsQualityRuntimeRebuildGuards =
                CountTextInMethod(terminalOsFile.Root, "RefreshScalabilityPolicy", "_nextQualityRefreshFrame") +
                CountTextInMethod(terminalOsFile.Root, "RefreshScalabilityPolicy", "targetResolution") +
                CountTextInMethod(terminalOsFile.Root, "RefreshScalabilityPolicy", "_textureResolution = targetResolution") +
                CountTextInMethod(terminalOsFile.Root, "RefreshScalabilityPolicy", "QueueGraphicsResourceRebuild()");
            summary.TerminalOsQualityPlayingTextureBlocks =
                CountTextInMethod(terminalOsFile.Root, "RefreshScalabilityPolicy", "_terminalTextureArray != null && Application.isPlaying");
            if (summary.TerminalOsQualityRuntimeRebuildGuards < 4 ||
                summary.TerminalOsQualityPlayingTextureBlocks != 0)
            {
                findings.Add(new Finding(
                    terminalOsFile.RelativePath,
                    0,
                    "terminal_os_quality_rebuild_route",
                    "TerminalOS quality changes must reach the visual-sync rebuild queue at runtime, guards=" +
                    summary.TerminalOsQualityRuntimeRebuildGuards +
                    " playing_texture_blocks=" +
                    summary.TerminalOsQualityPlayingTextureBlocks));
                summary.PhaseFindings++;
            }

            summary.MessageTerminalFiniteTimeGuards =
                CountTextInMethod(messageTerminalFile.Root, "Tick", "SanitizeDeltaTime(deltaTime)") +
                CountTextInMethod(messageTerminalFile.Root, "Tick", "SanitizeBlinkInterval(blinkInterval)") +
                CountTextInMethod(messageTerminalFile.Root, "StartPlayback", "ResolvePlaybackDuration(message)") +
                CountTextInMethod(messageTerminalFile.Root, "SanitizePositiveDuration", "float.IsNaN(durationSeconds)") +
                CountTextInMethod(messageTerminalFile.Root, "SanitizePositiveDuration", "MaxPlaybackDurationSeconds") +
                CountTextInMethod(messageTerminalFile.Root, "OnValidate", "SanitizePositiveDuration(entry.audioClip.length)");
            summary.MessageTerminalPresentationScalarGuards =
                CountTextInMethod(messageTerminalFile.Root, "QueueStaticAudio", "Sanitize01(volume)") +
                CountTextInMethod(messageTerminalFile.Root, "SanitizeBlinkInterval", "float.IsNaN(intervalSeconds)") +
                CountTextInMethod(messageTerminalFile.Root, "Sanitize01", "float.IsNaN(value)");
            summary.MessageTerminalPendingEventClears =
                CountTextInMethod(messageTerminalFile.Root, "OnDisable", "ClearQueuedTerminalEvents()") +
                CountTextInMethod(messageTerminalFile.Root, "OnDestroy", "ClearQueuedTerminalEvents()") +
                CountTextInMethod(messageTerminalFile.Root, "FlushQueuedTerminalEvents", "ClearQueuedTerminalEvents()");
            if (summary.MessageTerminalFiniteTimeGuards < 6 ||
                summary.MessageTerminalPresentationScalarGuards < 3 ||
                summary.MessageTerminalPendingEventClears < 3)
            {
                findings.Add(new Finding(
                    messageTerminalFile.RelativePath,
                    0,
                    "message_terminal_phase_hygiene",
                    "MessageTerminal must sanitize phase timers and clear queued legacy events on lifecycle exit, finite_guards=" +
                    summary.MessageTerminalFiniteTimeGuards +
                    " presentation_scalar_guards=" +
                    summary.MessageTerminalPresentationScalarGuards +
                    " pending_event_clears=" +
                    summary.MessageTerminalPendingEventClears));
                summary.PhaseFindings++;
            }

            summary.MessageTerminalMessageHashFields =
                CountTextInFile(messageTerminalFile, "public uint messageHash") +
                CountTextInFile(messageTerminalFile, "private uint[] _messageHashes") +
                CountTextInFile(messageTerminalFile, "private uint[] _readMessageHashes");
            summary.MessageTerminalMessageHashColdCaches =
                CountTextInMethod(messageTerminalFile.Root, "Awake", "CacheMessageHashesCold()") +
                CountTextInMethod(messageTerminalFile.Root, "AddMessage", "ResolveMessageHashCold(message)") +
                CountTextInMethod(messageTerminalFile.Root, "MarkMessageRead", "CacheMessageHashesCold()") +
                CountTextInMethod(messageTerminalFile.Root, "ApplyWfcOutpostDatapadLootedState", "CacheMessageHashesCold()") +
                CountTextInMethod(messageTerminalFile.Root, "RebuildReadMessageSetFromMessageStates", "CacheMessageHashesCold()");
            summary.MessageTerminalHashEventQueues =
                CountTextInMethod(messageTerminalFile.Root, "StartPlayback", "QueueMessageStartedEvent(messageHash, message.messageId)") +
                CountTextInMethod(messageTerminalFile.Root, "CompletePlayback", "QueueMessageCompletedEvent(messageHash, messageId)") +
                CountTextInMethod(messageTerminalFile.Root, "AddMessage", "QueueNewMessageReceivedEvent(messageHash, message.messageId)");
            summary.MessageTerminalHashEventFlushes =
                CountTextInMethod(messageTerminalFile.Root, "FlushQueuedTerminalEvents", "OnMessageStartedHash?.Invoke") +
                CountTextInMethod(messageTerminalFile.Root, "FlushQueuedTerminalEvents", "OnMessageCompletedHash?.Invoke") +
                CountTextInMethod(messageTerminalFile.Root, "FlushQueuedTerminalEvents", "OnNewMessageReceivedHash?.Invoke");
            summary.MessageTerminalHashEventClears =
                CountTextInMethod(messageTerminalFile.Root, "ClearQueuedTerminalEvents", "_pendingMessageStartedHash = 0u") +
                CountTextInMethod(messageTerminalFile.Root, "ClearQueuedTerminalEvents", "_pendingMessageCompletedHash = 0u") +
                CountTextInMethod(messageTerminalFile.Root, "ClearQueuedTerminalEvents", "_pendingNewMessageHash = 0u");
            summary.MessageTerminalHashPendingReads =
                CountTextInMethod(messageTerminalFile.Root, "UpdatePendingMessage", "IsReadMessageHash(messageHash)");
            summary.MessageTerminalLegacyPendingContains =
                CountTextInMethod(messageTerminalFile.Root, "UpdatePendingMessage", "_readMessageIds.Contains") +
                CountTextInMethod(messageTerminalFile.Root, "UpdatePendingMessage", "readMessageIds.Contains");

            if (summary.MessageTerminalMessageHashFields < 3 ||
                summary.MessageTerminalMessageHashColdCaches < 4 ||
                summary.MessageTerminalHashEventQueues < 3 ||
                summary.MessageTerminalHashEventFlushes < 3 ||
                summary.MessageTerminalHashEventClears < 3 ||
                summary.MessageTerminalHashPendingReads < 1 ||
                summary.MessageTerminalLegacyPendingContains != 0)
            {
                findings.Add(new Finding(
                    messageTerminalFile.RelativePath,
                    0,
                    "message_terminal_hash_route",
                    "MessageTerminal runtime read/event route must use cached uint hashes while legacy string events remain compatibility-only, fields=" +
                    summary.MessageTerminalMessageHashFields +
                    " cold_caches=" +
                    summary.MessageTerminalMessageHashColdCaches +
                    " hash_event_queues=" +
                    summary.MessageTerminalHashEventQueues +
                    " hash_event_flushes=" +
                    summary.MessageTerminalHashEventFlushes +
                    " hash_event_clears=" +
                    summary.MessageTerminalHashEventClears +
                    " pending_hash_reads=" +
                    summary.MessageTerminalHashPendingReads +
                    " legacy_pending_contains=" +
                    summary.MessageTerminalLegacyPendingContains));
                summary.ZeroGcFindings++;
            }
        }

        private static void ScanScannableFragmentLifecycleRoute(
            List<FileUnit> files,
            List<Finding> findings,
            ref ApexSummary summary)
        {
            if (!TryFindFile(files, "ScannableFragment.cs", out FileUnit scannableFile))
            {
                findings.Add(new Finding(
                    "ScannableFragment",
                    0,
                    "scannable_fragment_source_missing",
                    "ScannableFragment source is absent from Apex scope"));
                summary.DependencyFindings++;
                return;
            }

            summary.ScannableFragmentHashUnlocks =
                CountTextInMethod(scannableFile.Root, "TryUnlockLoreStage", "H8AppliedLoreRuntime.TryRaisePacketUnlockedAt");
            summary.ScannableFragmentLateFrameEventFlushes =
                CountInvocationInMethod(scannableFile.Root, "LateFrameTick", "FlushQueuedScanEvents");
            summary.ScannableFragmentLifecycleClears =
                CountTextInMethod(scannableFile.Root, "OnDisable", "ClearQueuedLateFrameWork()") +
                CountTextInMethod(scannableFile.Root, "OnDestroy", "ClearQueuedLateFrameWork()") +
                CountTextInMethod(scannableFile.Root, "ResetState", "ClearQueuedLateFrameWork()");
            summary.ScannableFragmentPendingStringClears =
                CountTextInMethod(scannableFile.Root, "ClearQueuedLateFrameWork", "_pendingCompleteEventUnlockId = null") +
                CountTextInMethod(scannableFile.Root, "FlushQueuedScanEvents", "_pendingCompleteEventUnlockId = null");
            summary.ScannableFragmentLockStateOrder = MethodTextAppearsInOrder(
                scannableFile.Root,
                "Lock",
                "StopScanning();",
                "_state = FragmentState.Locked;") ? 1 : 0;
            summary.ScannableFragmentEventFlushBeforeDisable = MethodTextAppearsInOrder(
                scannableFile.Root,
                "LateFrameTick",
                "FlushQueuedScanEvents();",
                "DisableFragment();") ? 1 : 0;

            if (summary.ScannableFragmentHashUnlocks != 1 ||
                summary.ScannableFragmentLateFrameEventFlushes != 1 ||
                summary.ScannableFragmentLifecycleClears < 3 ||
                summary.ScannableFragmentPendingStringClears < 2 ||
                summary.ScannableFragmentLockStateOrder != 1 ||
                summary.ScannableFragmentEventFlushBeforeDisable != 1)
            {
                findings.Add(new Finding(
                    scannableFile.RelativePath,
                    0,
                    "scannable_fragment_phase_hygiene",
                    "ScannableFragment must publish applied lore by hash, clear late-frame compatibility events before disable, and lock after scan stop, hash_unlocks=" +
                    summary.ScannableFragmentHashUnlocks +
                    " lateframe_flushes=" +
                    summary.ScannableFragmentLateFrameEventFlushes +
                    " lifecycle_clears=" +
                    summary.ScannableFragmentLifecycleClears +
                    " pending_string_clears=" +
                    summary.ScannableFragmentPendingStringClears +
                    " lock_order=" +
                    summary.ScannableFragmentLockStateOrder +
                    " flush_before_disable=" +
                    summary.ScannableFragmentEventFlushBeforeDisable));
                summary.PhaseFindings++;
            }
        }

        private static void ScanNarrativeDiscoveryHashCacheRoute(
            List<FileUnit> files,
            List<Finding> findings,
            ref ApexSummary summary)
        {
            if (!TryFindFile(files, "NarrativeDiscovery.cs", out FileUnit discoveryFile))
            {
                findings.Add(new Finding(
                    "NarrativeDiscovery",
                    0,
                    "narrative_discovery_source_missing",
                    "NarrativeDiscovery source is absent from Apex scope"));
                summary.DependencyFindings++;
                return;
            }

            summary.NarrativeDiscoveryLoreHashCaches =
                CountTextInFile(discoveryFile, "private uint _cachedLoreHash") +
                CountTextInMethod(discoveryFile.Root, "RefreshAupTriggerCache", "_cachedLoreHash = ComputeLoreHash(discoveryId)") +
                CountTextInMethod(discoveryFile.Root, "ComputeLoreHash", "LocHash.ComputeAscii(value)");
            summary.NarrativeDiscoveryCachedUnlockCalls =
                CountTextInMethod(discoveryFile.Root, "Interact", "loreUnlockSink.TryUnlockByHash(_cachedLoreHash)") +
                CountTextInMethod(discoveryFile.Root, "TryGetSpatialTrigger", "LoreHash = _cachedLoreHash");
            summary.NarrativeDiscoveryInteractionStringHashes =
                CountTextInMethod(discoveryFile.Root, "Interact", "LocHash.ComputeAscii(discoveryId)") +
                CountTextInMethod(discoveryFile.Root, "TryGetSpatialTrigger", "LocHash.ComputeAscii(discoveryId)");

            if (summary.NarrativeDiscoveryLoreHashCaches < 3 ||
                summary.NarrativeDiscoveryCachedUnlockCalls < 2 ||
                summary.NarrativeDiscoveryInteractionStringHashes != 0)
            {
                findings.Add(new Finding(
                    discoveryFile.RelativePath,
                    0,
                    "narrative_discovery_runtime_hash_cache",
                    "NarrativeDiscovery must use a cold cached lore hash for interaction/spatial routes, caches=" +
                    summary.NarrativeDiscoveryLoreHashCaches +
                    " cached_calls=" +
                    summary.NarrativeDiscoveryCachedUnlockCalls +
                    " runtime_string_hashes=" +
                    summary.NarrativeDiscoveryInteractionStringHashes));
                summary.ZeroGcFindings++;
            }
        }

        private static void ScanHectonNarrativeDirectorPoiHashCacheRoute(
            List<FileUnit> files,
            List<Finding> findings,
            ref ApexSummary summary)
        {
            if (!TryFindFile(files, "HectonNarrativeDirector.cs", out FileUnit directorFile) ||
                !TryFindFile(files, "HectonNarrativeDirector_PoiTriggers.cs", out FileUnit poiTriggersFile))
            {
                findings.Add(new Finding(
                    "HectonNarrativeDirector",
                    0,
                    "hecton_director_poi_hash_sources_missing",
                    "HectonNarrativeDirector POI source files are absent from Apex scope"));
                summary.DependencyFindings++;
                return;
            }

            summary.HectonDirectorPoiHashCaches =
                CountTextInFile(directorFile, "private readonly uint[] _poiDiscoveryHashes") +
                CountTextInMethod(directorFile.Root, "RebuildNativePoiRegistry", "Array.Clear(_poiDiscoveryHashes") +
                CountTextInMethod(directorFile.Root, "RebuildNativePoiRegistry", "_poiDiscoveryHashes[discoveryIdCount] = trigger.PoiHash") +
                CountTextInMethod(directorFile.Root, "GetNearestUndiscoveredPOI", "uint discoveryHash = poi.DiscoveryHash");
            summary.HectonDirectorPoiCachedDispatches =
                CountTextInMethod(poiTriggersFile.Root, "DispatchAupNarrativePoiSolvedResult", "_poiDiscoveryHashes[poiIndex]");
            summary.HectonDirectorPoiRuntimeStringHashes =
                CountTextInMethod(directorFile.Root, "GetNearestUndiscoveredPOI", "NarrativeEvents.ComputeDiscoveryHash(poi.DiscoveryId)") +
                CountTextInMethod(poiTriggersFile.Root, "DispatchAupNarrativePoiSolvedResult", "NarrativeEvents.ComputeDiscoveryHash(discoveryId)");

            if (summary.HectonDirectorPoiHashCaches < 4 ||
                summary.HectonDirectorPoiCachedDispatches < 1 ||
                summary.HectonDirectorPoiRuntimeStringHashes != 0)
            {
                findings.Add(new Finding(
                    directorFile.RelativePath,
                    0,
                    "hecton_director_poi_runtime_hash_cache",
                    "HectonNarrativeDirector must use cold cached POI hashes for selection/dispatch, caches=" +
                    summary.HectonDirectorPoiHashCaches +
                    " cached_dispatches=" +
                    summary.HectonDirectorPoiCachedDispatches +
                    " runtime_string_hashes=" +
                    summary.HectonDirectorPoiRuntimeStringHashes));
                summary.ZeroGcFindings++;
            }
        }

        private static void ScanAppliedLoreWorldImpactPhaseRoute(
            List<FileUnit> files,
            List<Finding> findings,
            ref ApexSummary summary)
        {
            if (!TryFindFile(files, "HectonNarrativeDirector.cs", out FileUnit directorFile) ||
                !TryFindFile(files, "HectonNarrativeDirector_PoiTriggers.cs", out FileUnit poiTriggersFile) ||
                !TryFindFile(files, "H8DataMonolithTypes.cs", out FileUnit dataLayoutFile) ||
                !TryFindFile(files, "H8StaticDataArena.cs", out FileUnit staticArenaFile) ||
                !TryFindFile(files, "H8AppliedLoreRuntime.cs", out FileUnit appliedLoreRuntimeFile))
            {
                findings.Add(new Finding(
                    "AppliedLoreWorldImpact",
                    0,
                    "applied_lore_world_impact_sources_missing",
                    "Applied lore world-impact source files are absent from Apex scope"));
                summary.DependencyFindings++;
                return;
            }

            summary.AppliedLoreWorldImpactTickDrains =
                CountInvocationInMethod(poiTriggersFile.Root, "Tick", "ConsumeAppliedLoreWorldImpactSignals");
            summary.AppliedLoreWorldImpactLateFrameDrains =
                CountInvocationInMethod(poiTriggersFile.Root, "LateFrameTick", "ConsumeAppliedLoreWorldImpactSignals");
            summary.AppliedLoreWorldImpactQueuedAudioTransfers =
                CountTextInFile(poiTriggersFile, "private float _pendingAppliedLoreAudioGhost01") +
                CountTextInFile(poiTriggersFile, "private bool _hasPendingAppliedLoreAudioGhost") +
                CountTextInMethod(poiTriggersFile.Root, "ConsumeAppliedLoreWorldImpactSignals", "QueueAppliedLoreAudioGhost(acousticInterference01)") +
                CountTextInMethod(poiTriggersFile.Root, "FlushAppliedLoreAudioGhostVisualSync", "SetNarrativeRadioInterference");
            summary.AppliedLoreWorldImpactLifecycleClears =
                CountTextInMethod(directorFile.Root, "OnDisable", "ClearAppliedLoreWorldImpactState()") +
                CountTextInMethod(directorFile.Root, "OnDestroy", "ClearAppliedLoreWorldImpactState()") +
                CountTextInMethod(poiTriggersFile.Root, "ClearAppliedLoreWorldImpactState", "_hasPendingAppliedLoreAudioGhost = false");
            summary.AppliedLoreWorldImpactSignalPublishes =
                CountTextInMethod(appliedLoreRuntimeFile.Root, "TryRaiseScanCompleteWorldImpact", "SignalBus<BiomeChangedSignal>.TryPushTracked") +
                CountTextInMethod(appliedLoreRuntimeFile.Root, "TryRaiseScanCompleteWorldImpact", "SignalBus<ToolAcousticSignal>.TryPushTracked");
            summary.AppliedLoreWorldImpactDedupGuards =
                CountTextInFile(poiTriggersFile, "private uint _lastAppliedLoreImpactEntryHash") +
                CountTextInFile(poiTriggersFile, "private uint _lastAppliedLoreImpactScanId") +
                CountTextInFile(poiTriggersFile, "private uint _lastAppliedLoreImpactSourceId") +
                CountTextInMethod(poiTriggersFile.Root, "ConsumeAppliedLoreWorldImpactSignals", "IsDuplicateAppliedLoreWorldImpact") +
                CountTextInMethod(poiTriggersFile.Root, "ConsumeAppliedLoreWorldImpactSignals", "CacheAppliedLoreWorldImpactSignal") +
                CountTextInMethod(poiTriggersFile.Root, "ClearAppliedLoreWorldImpactState", "_lastAppliedLoreImpactEntryHash = 0u") +
                CountTextInMethod(poiTriggersFile.Root, "ClearAppliedLoreWorldImpactState", "_lastAppliedLoreImpactScanId = 0u") +
                CountTextInMethod(poiTriggersFile.Root, "ClearAppliedLoreWorldImpactState", "_lastAppliedLoreImpactSourceId = 0u");
            summary.AppliedLoreWorldImpactLayoutSizeConstants =
                CountTextInFile(appliedLoreRuntimeFile, "public const int SizeBytes = 24");
            summary.AppliedLoreWorldImpactLayoutPaddingFields =
                CountTextInFile(appliedLoreRuntimeFile, "[FieldOffset(17)]") +
                CountTextInFile(appliedLoreRuntimeFile, "[FieldOffset(18)]") +
                CountTextInFile(appliedLoreRuntimeFile, "[FieldOffset(20)]");
            summary.AppliedLoreWorldImpactLayoutSizeofProofs =
                CountTextInFile(appliedLoreRuntimeFile, "using Unity.Collections.LowLevel.Unsafe;") +
                CountTextInMethod(appliedLoreRuntimeFile.Root, "ValidateRuntimeLayout", "UnsafeUtility.SizeOf<H8AppliedLoreWorldImpactRecord>()") +
                CountTextInMethod(appliedLoreRuntimeFile.Root, "ValidateRuntimeLayout", "H8AppliedLoreWorldImpactRecord.SizeBytes") +
                CountTextInMethod(appliedLoreRuntimeFile.Root, "ValidateRuntimeLayout", "(worldImpactBytes & 7) == 0");
            summary.AppliedLoreWorldImpactCentralAuditProofs =
                CountTextInMethod(dataLayoutFile.Root, "ValidateBlittableSizes", "UnsafeUtility.SizeOf<H8AppliedLoreWorldImpactRecord>()") +
                CountTextInMethod(dataLayoutFile.Root, "ValidateBlittableSizes", "H8AppliedLoreWorldImpactRecord.SizeBytes") +
                CountTextInMethod(dataLayoutFile.Root, "ValidateBlittableSizes", "(UnsafeUtility.SizeOf<H8AppliedLoreWorldImpactRecord>() & 7) == 0");
            summary.AppliedLoreUtf8PassByRefProofs =
                CountTextInFile(appliedLoreRuntimeFile, "H8StaticDataArena.TryGetAppliedLoreUtf8(in record, surface, out utf8Bytes)") +
                CountTextInFile(staticArenaFile, "public static bool TryGetAppliedLoreUtf8(") +
                CountTextInFile(staticArenaFile, "in H8AppliedLorePacketRecord record");
            summary.AppliedLoreUtf8FacadeDuplicateSelectors =
                CountTextInFile(appliedLoreRuntimeFile, "case H8AppliedLoreSurface.") +
                CountTextInFile(appliedLoreRuntimeFile, "TryGetLocalizedUtf8Span(") +
                CountTextInFile(appliedLoreRuntimeFile, "TryGetUtf8FromRecord(");

            if (summary.AppliedLoreWorldImpactTickDrains != 1 ||
                summary.AppliedLoreWorldImpactLateFrameDrains != 0 ||
                summary.AppliedLoreWorldImpactQueuedAudioTransfers < 4 ||
                summary.AppliedLoreWorldImpactLifecycleClears < 3 ||
                summary.AppliedLoreWorldImpactSignalPublishes < 2 ||
                summary.AppliedLoreWorldImpactDedupGuards < 8 ||
                summary.AppliedLoreWorldImpactLayoutSizeConstants < 1 ||
                summary.AppliedLoreWorldImpactLayoutPaddingFields < 3 ||
                summary.AppliedLoreWorldImpactLayoutSizeofProofs < 4 ||
                summary.AppliedLoreWorldImpactCentralAuditProofs < 3 ||
                summary.AppliedLoreUtf8PassByRefProofs < 4 ||
                summary.AppliedLoreUtf8FacadeDuplicateSelectors != 0)
            {
                findings.Add(new Finding(
                    poiTriggersFile.RelativePath,
                    0,
                    "applied_lore_world_impact_phase_route",
                    "Applied-lore world impact must publish world signals from Tick and defer only audio sink transfer to LateFrameTick, tick_drains=" +
                    summary.AppliedLoreWorldImpactTickDrains +
                    " lateframe_drains=" +
                    summary.AppliedLoreWorldImpactLateFrameDrains +
                    " queued_audio_transfers=" +
                    summary.AppliedLoreWorldImpactQueuedAudioTransfers +
                    " lifecycle_clears=" +
                    summary.AppliedLoreWorldImpactLifecycleClears +
                    " signal_publishes=" +
                    summary.AppliedLoreWorldImpactSignalPublishes +
                    " dedup_guards=" +
                    summary.AppliedLoreWorldImpactDedupGuards +
                    " layout_size_constants=" +
                    summary.AppliedLoreWorldImpactLayoutSizeConstants +
                    " layout_padding_fields=" +
                    summary.AppliedLoreWorldImpactLayoutPaddingFields +
                    " layout_sizeof_proofs=" +
                    summary.AppliedLoreWorldImpactLayoutSizeofProofs +
                    " central_audit_proofs=" +
                    summary.AppliedLoreWorldImpactCentralAuditProofs +
                    " utf8_pass_by_ref_proofs=" +
                    summary.AppliedLoreUtf8PassByRefProofs +
                    " utf8_facade_duplicate_selectors=" +
                    summary.AppliedLoreUtf8FacadeDuplicateSelectors));
                summary.PhaseFindings++;
            }
        }

        private static void ScanMetaCampaignPhaseSideEffectRoute(
            List<FileUnit> files,
            List<Finding> findings,
            ref ApexSummary summary)
        {
            if (!TryFindFile(files, "MetaCampaignService.cs", out FileUnit campaignFile))
            {
                findings.Add(new Finding(
                    "MetaCampaignService",
                    0,
                    "meta_campaign_visual_phase_source_missing",
                    "MetaCampaignService.cs is absent from Apex scope"));
                summary.DependencyFindings++;
                return;
            }

            summary.MetaCampaignVisualQueueCalls =
                CountInvocationInMethod(campaignFile.Root, "OnEnable", "QueueCachedVisualState") +
                CountInvocationInMethod(campaignFile.Root, "PublishStateSideEffects", "QueueCachedVisualState");
            summary.MetaCampaignVisualFlushLateFrameCalls =
                CountInvocationInMethod(campaignFile.Root, "LateFrameTick", "FlushCachedVisualState");
            summary.MetaCampaignVisualPublishCalls =
                CountInvocationInMethod(campaignFile.Root, "FlushCachedVisualState", "PublishCachedVisualState");
            summary.MetaCampaignVisualShaderWrites =
                CountTextInMethod(campaignFile.Root, "PublishCachedVisualState", "Shader.SetGlobalFloat") +
                CountTextInMethod(campaignFile.Root, "PublishCachedVisualState", "ApplyCampaignToxicityPressure");
            int directVisualPublishCalls =
                CountTextInFile(campaignFile, "PublishCachedVisualState(") -
                CountTextInFile(campaignFile, "private void PublishCachedVisualState(") -
                summary.MetaCampaignVisualPublishCalls;

            summary.MetaCampaignAudioQueueCalls =
                CountInvocationInMethod(campaignFile.Root, "PublishStateSideEffects", "QueueCampaignBroadcast");
            summary.MetaCampaignAudioFlushLateFrameCalls =
                CountInvocationInMethod(campaignFile.Root, "LateFrameTick", "FlushCampaignBroadcast");
            summary.MetaCampaignAudioPublishCalls =
                CountInvocationInMethod(campaignFile.Root, "FlushCampaignBroadcast", "PublishCampaignBroadcast");
            int directAudioPublishCalls =
                CountTextInFile(campaignFile, "PublishCampaignBroadcast(") -
                CountTextInFile(campaignFile, "private void PublishCampaignBroadcast(") -
                summary.MetaCampaignAudioPublishCalls;

            summary.MetaCampaignCartographyQueueCalls =
                CountInvocationInMethod(campaignFile.Root, "PublishStateSideEffects", "QueueCartographyState");
            summary.MetaCampaignCartographyFlushLateFrameCalls =
                CountInvocationInMethod(campaignFile.Root, "LateFrameTick", "FlushCartographyState");
            summary.MetaCampaignCartographyPublishCalls =
                CountInvocationInMethod(campaignFile.Root, "FlushCartographyState", "PublishCartographyState");
            int directCartographyPublishCalls =
                CountTextInFile(campaignFile, "PublishCartographyState(") -
                CountTextInFile(campaignFile, "private void PublishCartographyState(") -
                summary.MetaCampaignCartographyPublishCalls;

            if (summary.MetaCampaignVisualQueueCalls != 2 ||
                summary.MetaCampaignVisualFlushLateFrameCalls != 1 ||
                summary.MetaCampaignVisualPublishCalls != 1 ||
                summary.MetaCampaignVisualShaderWrites != 2 ||
                directVisualPublishCalls != 0 ||
                summary.MetaCampaignAudioQueueCalls != 1 ||
                summary.MetaCampaignAudioFlushLateFrameCalls != 1 ||
                summary.MetaCampaignAudioPublishCalls != 1 ||
                directAudioPublishCalls != 0 ||
                summary.MetaCampaignCartographyQueueCalls != 1 ||
                summary.MetaCampaignCartographyFlushLateFrameCalls != 1 ||
                summary.MetaCampaignCartographyPublishCalls != 1 ||
                directCartographyPublishCalls != 0)
            {
                findings.Add(new Finding(
                    campaignFile.RelativePath,
                    0,
                    "meta_campaign_phase_side_effect_route",
                    "MetaCampaign presentation side effects must queue from state changes and flush only in LateFrameTick, visual_queue_calls=" +
                    summary.MetaCampaignVisualQueueCalls +
                    " visual_lateframe_flushes=" +
                    summary.MetaCampaignVisualFlushLateFrameCalls +
                    " visual_publish_calls=" +
                    summary.MetaCampaignVisualPublishCalls +
                    " shader_writes=" +
                    summary.MetaCampaignVisualShaderWrites +
                    " visual_direct_publishes=" +
                    directVisualPublishCalls +
                    " audio_queue_calls=" +
                    summary.MetaCampaignAudioQueueCalls +
                    " audio_lateframe_flushes=" +
                    summary.MetaCampaignAudioFlushLateFrameCalls +
                    " audio_publish_calls=" +
                    summary.MetaCampaignAudioPublishCalls +
                    " audio_direct_publishes=" +
                    directAudioPublishCalls +
                    " cartography_queue_calls=" +
                    summary.MetaCampaignCartographyQueueCalls +
                    " cartography_lateframe_flushes=" +
                    summary.MetaCampaignCartographyFlushLateFrameCalls +
                    " cartography_publish_calls=" +
                    summary.MetaCampaignCartographyPublishCalls +
                    " cartography_direct_publishes=" +
                    directCartographyPublishCalls));
                summary.PhaseFindings++;
            }
        }

        private static void ScanPrologueBlackBoxDataVaultRoute(
            List<FileUnit> files,
            List<Finding> findings,
            ref ApexSummary summary)
        {
            if (!TryFindFile(files, "AwaitableDropSequenceDirector.cs", out FileUnit prologueFile))
            {
                findings.Add(new Finding(
                    "AwaitableDropSequenceDirector",
                    0,
                    "prologue_blackbox_source_missing",
                    "AwaitableDropSequenceDirector.cs is absent from Apex scope"));
                summary.DependencyFindings++;
                return;
            }

            if (!TryFindMethod(prologueFile.Root, "RecordStage", out MethodDeclarationSyntax recordStage))
            {
                findings.Add(new Finding(
                    prologueFile.RelativePath,
                    0,
                    "prologue_blackbox_record_stage_missing",
                    "RecordStage is required for prologue black-box telemetry"));
                summary.DependencyFindings++;
                return;
            }

            summary.PrologueBlackBoxWriteLocksChecked =
                CountInvocationInMethod(prologueFile.Root, "RecordStage", "TryAcquireWriteLock");
            summary.PrologueBlackBoxReleaseFinallyProofs =
                CountDataVaultWriteReleaseFinallyInMethod(recordStage);
            summary.PrologueBlackBoxHoistedTelemetryProofs =
                CountTextInMethod(prologueFile.Root, "RecordStage", "IPrologueSequenceRuntime runtime = _runtime") +
                CountTextInMethod(prologueFile.Root, "RecordStage", "uint telemetryFrame = runtime != null ? runtime.CurrentFrame : 0u") +
                CountTextInMethod(prologueFile.Root, "RecordStage", "ushort telemetrySequence =") +
                CountTextInMethod(prologueFile.Root, "RecordStage", "ResolveTelemetrySpeedMetersPerSecond()") +
                CountTextInMethod(prologueFile.Root, "RecordStage", "int blackBoxIndex = math.clamp") +
                CountTextInMethod(prologueFile.Root, "RecordStage", "int nextBlackBoxCursor =");
            summary.PrologueBlackBoxHeavyInsideWriteLock =
                CountHeavyTelemetryInsideDataVaultWriteReleaseTry(recordStage);

            if (summary.PrologueBlackBoxWriteLocksChecked != 1 ||
                summary.PrologueBlackBoxReleaseFinallyProofs != 1 ||
                summary.PrologueBlackBoxHoistedTelemetryProofs < 6 ||
                summary.PrologueBlackBoxHeavyInsideWriteLock != 0)
            {
                findings.Add(new Finding(
                    prologueFile.RelativePath,
                    LineOf(recordStage),
                    "prologue_blackbox_write_lock_shape",
                    "Prologue black-box telemetry must hoist runtime snapshots/math before DataVault write lock, locks=" +
                    summary.PrologueBlackBoxWriteLocksChecked +
                    " release_finally=" +
                    summary.PrologueBlackBoxReleaseFinallyProofs +
                    " hoisted_proofs=" +
                    summary.PrologueBlackBoxHoistedTelemetryProofs +
                    " heavy_inside_write_lock=" +
                    summary.PrologueBlackBoxHeavyInsideWriteLock));
                summary.LockFindings++;
            }
        }

        private static void ScanPdaTelemetryVaultRoute(
            List<FileUnit> files,
            List<Finding> findings,
            ref ApexSummary summary)
        {
            if (!TryFindFile(files, "PDAEncyclopediaStreamer.cs", out FileUnit pdaFile))
            {
                findings.Add(new Finding(
                    "PDAEncyclopediaStreamer",
                    0,
                    "pda_telemetry_source_missing",
                    "PDAEncyclopediaStreamer.cs is absent from Apex scope"));
                summary.DependencyFindings++;
                return;
            }

            if (!TryFindMethod(pdaFile.Root, "RecordTelemetry", out MethodDeclarationSyntax recordTelemetry))
            {
                findings.Add(new Finding(
                    pdaFile.RelativePath,
                    0,
                    "pda_telemetry_record_method_missing",
                    "RecordTelemetry is required for PDA black-box/runtime telemetry"));
                summary.DependencyFindings++;
                return;
            }

            if (!TryFindMethod(pdaFile.Root, "WriteBlackBoxDump", out MethodDeclarationSyntax writeBlackBoxDump))
            {
                findings.Add(new Finding(
                    pdaFile.RelativePath,
                    0,
                    "pda_blackbox_dump_method_missing",
                    "WriteBlackBoxDump is required for PDA crash telemetry"));
                summary.DependencyFindings++;
                return;
            }

            summary.PdaTelemetryWriteLocksChecked =
                CountInvocationInMethod(pdaFile.Root, "RecordTelemetry", "TryAcquireWriteLock");
            summary.PdaTelemetryReleaseFinallyProofs =
                CountDataVaultWriteReleaseFinallyInMethod(recordTelemetry);
            summary.PdaTelemetryReadOnlyTelemetrySnapshots =
                CountTextInMethod(pdaFile.Root, "RecordTelemetry", "TryReadVaultBuffer(in _telemetryHandle") +
                CountTextInMethod(pdaFile.Root, "RecordTelemetry", "telemetrySnapshot");
            summary.PdaTelemetryWriteLockSizeProofs =
                CountTextInMethod(pdaFile.Root, "RecordTelemetry", "telemetry.Length < TelemetryFrameCount");
            summary.PdaTelemetryRuntimeStateFallbackReads =
                CountTextInMethod(pdaFile.Root, "RecordTelemetry", "TryReadVaultBuffer(in _runtimeStateHandle");
            summary.PdaTelemetryStreamingSnapshotPasses =
                CountTextInMethod(pdaFile.Root, "LateFrameTick", "WriteRuntimeState(quality, decodeTicks, canvasTicks, out uint unlockedCountSnapshot)") +
                CountTextInMethod(pdaFile.Root, "LateFrameTick", "RecordTelemetry(charsRenderedThisFrame, decodeTicks, canvasTicks, unlockedCountSnapshot, hasRuntimeStateSnapshot)") +
                CountTextInMethod(pdaFile.Root, "RecordTelemetry", "bool hasUnlockedCountSnapshot = false") +
                CountTextInMethod(pdaFile.Root, "RecordTelemetry", "entry.UnlockedCount = unlockedCountSnapshot");
            summary.PdaBlackBoxDumpSingleTelemetrySnapshots =
                CountTextInMethod(pdaFile.Root, "WriteBlackBoxDump", "NativeArray<PdaEncyclopediaTelemetryEntry>.ReadOnly telemetrySnapshot") +
                CountTextInMethod(pdaFile.Root, "WriteBlackBoxDump", "TryReadVaultBuffer(in _telemetryHandle");
            summary.PdaBlackBoxDumpPerRowTelemetryReads =
                CountTextInMethod(pdaFile.Root, "WriteBlackBoxDump", "TryReadTelemetryDumpEntry") +
                CountTextInMethod(pdaFile.Root, "TryReadTelemetryDumpEntry", "TryReadVaultBuffer(in _telemetryHandle");
            summary.PdaBlackBoxDumpTransientPayloads =
                CountTextInMethod(pdaFile.Root, "WriteBlackBoxDump", "NativeFaultDumpWriter.CreateTransientPayload") +
                CountTextInMethod(pdaFile.Root, "WriteBlackBoxDump", "NativeFaultDumpWriter.DisposeTransientPayload");
            summary.PdaBlackBoxDumpRawPayloadAllocs =
                CountTextInMethod(pdaFile.Root, "WriteBlackBoxDump", "new NativeArray<byte>");

            if (summary.PdaTelemetryWriteLocksChecked != 2 ||
                summary.PdaTelemetryReleaseFinallyProofs != 2 ||
                summary.PdaTelemetryReadOnlyTelemetrySnapshots != 0 ||
                summary.PdaTelemetryWriteLockSizeProofs != 1 ||
                summary.PdaTelemetryRuntimeStateFallbackReads != 1 ||
                summary.PdaTelemetryStreamingSnapshotPasses < 4 ||
                summary.PdaBlackBoxDumpSingleTelemetrySnapshots != 2 ||
                summary.PdaBlackBoxDumpPerRowTelemetryReads != 0 ||
                summary.PdaBlackBoxDumpTransientPayloads != 2 ||
                summary.PdaBlackBoxDumpRawPayloadAllocs != 0)
            {
                findings.Add(new Finding(
                    pdaFile.RelativePath,
                    LineOf(writeBlackBoxDump),
                    "pda_telemetry_vault_route_shape",
                    "PDA telemetry must avoid redundant read-only telemetry ring lookup before write-lock, write_locks=" +
                    summary.PdaTelemetryWriteLocksChecked +
                    " release_finally=" +
                    summary.PdaTelemetryReleaseFinallyProofs +
                    " redundant_readonly=" +
                    summary.PdaTelemetryReadOnlyTelemetrySnapshots +
                    " size_proofs=" +
                    summary.PdaTelemetryWriteLockSizeProofs +
                    " runtime_fallback_reads=" +
                    summary.PdaTelemetryRuntimeStateFallbackReads +
                    " streaming_snapshot_passes=" +
                    summary.PdaTelemetryStreamingSnapshotPasses +
                    " dump_single_snapshots=" +
                    summary.PdaBlackBoxDumpSingleTelemetrySnapshots +
                    " dump_per_row_reads=" +
                    summary.PdaBlackBoxDumpPerRowTelemetryReads +
                    " dump_transient_payloads=" +
                    summary.PdaBlackBoxDumpTransientPayloads +
                    " dump_raw_payload_allocs=" +
                    summary.PdaBlackBoxDumpRawPayloadAllocs));
                summary.LockFindings++;
            }
        }

        private static void ScanTerminalOsTelemetryVaultRoute(
            List<FileUnit> files,
            List<Finding> findings,
            ref ApexSummary summary)
        {
            bool hasTerminalFile = TryFindFile(files, "TerminalOsRuntime.cs", out FileUnit terminalFile);
            bool hasProjectionFile = TryFindFile(files, "TerminalOsRuntime_TerminalProjection.cs", out FileUnit projectionFile);
            if (!hasTerminalFile || !hasProjectionFile)
            {
                findings.Add(new Finding(
                    "TerminalOsRuntime",
                    0,
                    "terminal_os_telemetry_source_missing",
                    "TerminalOsRuntime telemetry sources are absent from Apex scope, terminal=" +
                    hasTerminalFile +
                    " projection=" +
                    hasProjectionFile));
                summary.DependencyFindings++;
                return;
            }

            if (!TryFindMethod(terminalFile.Root, "RecordTelemetry", out MethodDeclarationSyntax recordTelemetry) ||
                !TryFindMethod(terminalFile.Root, "RecordDecryptionTelemetry", out MethodDeclarationSyntax recordDecryptionTelemetry) ||
                !TryFindMethod(projectionFile.Root, "RecordTerminalInputTelemetry", out MethodDeclarationSyntax recordTerminalInputTelemetry))
            {
                findings.Add(new Finding(
                    terminalFile.RelativePath,
                    0,
                    "terminal_os_telemetry_methods_missing",
                    "RecordTelemetry, RecordDecryptionTelemetry and RecordTerminalInputTelemetry are required for terminal black-box telemetry"));
                summary.DependencyFindings++;
                return;
            }

            string decryptionText = recordDecryptionTelemetry.ToFullString();
            string terminalInputText = recordTerminalInputTelemetry.ToFullString();
            int puzzleOpenIndex = decryptionText.IndexOf(
                "TryOpenVaultBuffer(ref _decryptionPuzzlesHandle",
                StringComparison.Ordinal);
            int ringOpenIndex = decryptionText.IndexOf(
                "TryOpenVaultBuffer(ref _decryptionTelemetryRingHandle",
                StringComparison.Ordinal);
            int inputFaultIndex = terminalInputText.IndexOf(
                "uint projectionFaults = ownerFaultFlags;",
                StringComparison.Ordinal);
            int inputRingOpenIndex = terminalInputText.IndexOf(
                "TryOpenVaultBuffer(ref _terminalInputTelemetryRingHandle",
                StringComparison.Ordinal);

            summary.TerminalOsTelemetryLayoutHashHoists =
                CountTextInMethod(terminalFile.Root, "RecordTelemetry", "uint layoutHashSnapshot = ComputeLayoutHash();") +
                CountTextInMethod(terminalFile.Root, "RecordTelemetry", "LayoutHash = layoutHashSnapshot");
            summary.TerminalOsTelemetryRingOpenAfterSnapshots =
                CountTextInMethod(terminalFile.Root, "RecordTelemetry", "TryOpenVaultBuffer(ref _telemetryRingHandle") +
                CountTextInMethod(terminalFile.Root, "RecordTelemetry", "int telemetryIndex = math.clamp(_telemetryCursor") +
                CountTextInMethod(terminalFile.Root, "RecordTelemetry", "_telemetryCursor = (telemetryIndex + 1) % telemetryRing.Length");
            summary.TerminalOsTelemetryRingLengthGuards =
                CountTextInMethod(terminalFile.Root, "RecordTelemetry", "telemetryRing.Length == 0") +
                CountTextInMethod(terminalFile.Root, "RecordDecryptionTelemetry", "telemetryRing.Length == 0");
            summary.TerminalOsDecryptionTelemetrySnapshotBeforeRing =
                puzzleOpenIndex >= 0 && ringOpenIndex > puzzleOpenIndex ? 1 : 0;
            summary.TerminalOsDecryptionTelemetryCursorClamps =
                CountTextInMethod(terminalFile.Root, "RecordDecryptionTelemetry", "int telemetryIndex = math.clamp(_decryptionTelemetryCursor") +
                CountTextInMethod(terminalFile.Root, "RecordDecryptionTelemetry", "_decryptionTelemetryCursor = (telemetryIndex + 1) % telemetryRing.Length");
            summary.TerminalOsInputTelemetryFaultsBeforeRing =
                inputFaultIndex >= 0 && inputRingOpenIndex > inputFaultIndex ? 1 : 0;
            summary.TerminalOsInputTelemetryCursorClamps =
                CountTextInMethod(projectionFile.Root, "RecordTerminalInputTelemetry", "int telemetryIndex = math.clamp(_terminalInputTelemetryCursor") +
                CountTextInMethod(projectionFile.Root, "RecordTerminalInputTelemetry", "_terminalInputTelemetryCursor = (telemetryIndex + 1) % telemetryRing.Length");

            if (summary.TerminalOsTelemetryLayoutHashHoists < 2 ||
                summary.TerminalOsTelemetryRingOpenAfterSnapshots < 3 ||
                summary.TerminalOsTelemetryRingLengthGuards < 2 ||
                summary.TerminalOsDecryptionTelemetrySnapshotBeforeRing != 1 ||
                summary.TerminalOsDecryptionTelemetryCursorClamps < 2 ||
                summary.TerminalOsInputTelemetryFaultsBeforeRing != 1 ||
                summary.TerminalOsInputTelemetryCursorClamps < 2)
            {
                findings.Add(new Finding(
                    terminalFile.RelativePath,
                    LineOf(recordTelemetry),
                    "terminal_os_telemetry_vault_route_shape",
                    "TerminalOS telemetry must hoist read snapshots before telemetry ring writes, layout_hoists=" +
                    summary.TerminalOsTelemetryLayoutHashHoists +
                    " ring_after_snapshots=" +
                    summary.TerminalOsTelemetryRingOpenAfterSnapshots +
                    " ring_length_guards=" +
                    summary.TerminalOsTelemetryRingLengthGuards +
                    " decryption_snapshot_before_ring=" +
                    summary.TerminalOsDecryptionTelemetrySnapshotBeforeRing +
                    " decryption_cursor_clamps=" +
                    summary.TerminalOsDecryptionTelemetryCursorClamps +
                    " input_faults_before_ring=" +
                    summary.TerminalOsInputTelemetryFaultsBeforeRing +
                    " input_cursor_clamps=" +
                    summary.TerminalOsInputTelemetryCursorClamps));
                summary.LockFindings++;
            }
        }

        private static void ScanPdaCorruptedRecordRoute(
            List<FileUnit> files,
            List<Finding> findings,
            ref ApexSummary summary)
        {
            FileUnit pdaFile;
            if (!TryFindFile(files, "PDAEncyclopediaStreamer.cs", out pdaFile))
            {
                findings.Add(new Finding(
                    "PDAEncyclopediaStreamer",
                    0,
                    "pda_corrupted_route_missing",
                    "PDAEncyclopediaStreamer source is absent from Apex scope"));
                summary.DependencyFindings++;
                return;
            }

            summary.PdaCorruptedWriterDefinitions = CountTextInFile(pdaFile, "private void WriteCorruptedBody(uint hash)");
            summary.PdaCorruptedLateFrameCalls = CountInvocationInMethod(
                pdaFile.Root,
                "LateFrameTick",
                "WriteCorruptedBody");
            summary.PdaCorruptedBodySpanWrites =
                CountTextInMethod(pdaFile.Root, "WriteCorruptedBody", "\"[CORRUPTED DATA RECORD] \".AsSpan()") +
                CountTextInMethod(pdaFile.Root, "WriteCorruptedBody", "AppendHex8") +
                CountTextInMethod(pdaFile.Root, "WriteCorruptedBody", "SubmitBodyIfChanged");
            summary.PdaRuntimeTmpStringWrites =
                CountTextInFile(pdaFile, "bodyText.text") +
                CountTextInFile(pdaFile, "bodyText.SetText(");
            summary.PdaFiniteQualityResolvers = CountTextInFile(pdaFile, "private float ResolveGlobalQualityWeight01()");
            summary.PdaFiniteQualityCalls = CountTextInFile(pdaFile, "ResolveGlobalQualityWeight01()");
            summary.PdaRawQualitySaturates = CountTextInFile(pdaFile, "math.saturate(HomeostasisBrain.GlobalQualityWeight)");
            summary.PdaFiniteQualityGuards =
                CountTextInMethod(pdaFile.Root, "ResolveGlobalQualityWeight01", "math.isfinite(quality)") +
                CountTextInMethod(pdaFile.Root, "ResolveGlobalQualityWeight01", "return 0.5f");
            summary.PdaInstantRevealContracts =
                CountTextInFile(pdaFile, "public void RequestInstantReveal()") +
                CountInvocationInMethod(pdaFile.Root, "LateFrameTick", "ForceRevealDecodedTextIfRequested") +
                CountTextInMethod(pdaFile.Root, "ForceRevealDecodedTextIfRequested", "_visibleLength = decoded") +
                CountTextInMethod(pdaFile.Root, "ForceRevealDecodedTextIfRequested", "_charAccumulator = 0f");
            summary.PdaInstantRevealLifecycleClears =
                CountTextInMethod(pdaFile.Root, "OnDisable", "_forceRevealDecodedTextNextVisualSync = false") +
                CountTextInMethod(pdaFile.Root, "BeginEntry", "_forceRevealDecodedTextNextVisualSync = false");
            summary.PdaUiRescaleColdInitializers =
                CountTextInMethod(pdaFile.Root, "OnEnable", "SignalBus<UIRescaleRequestSignal>.EnsureInitialized()") +
                CountInvocationInMethod(pdaFile.Root, "OnEnable", "CapturePdaTextFontBaselinesCold");
            summary.PdaUiRescaleLateFrameCalls =
                CountInvocationInMethod(pdaFile.Root, "LateFrameTick", "ConsumeUiRescaleRequestsVisualSync");
            summary.PdaUiRescaleSnapshotReads =
                CountInvocationTextInMethod(pdaFile.Root, "ConsumeUiRescaleRequestsVisualSync", "SignalBus<UIRescaleRequestSignal>.GetFrameSnapshot");
            summary.PdaUiRescaleFiniteGuards =
                CountTextInMethod(pdaFile.Root, "ResolvePdaTextScale", "math.isfinite") +
                CountTextInMethod(pdaFile.Root, "ResolvePdaTextScale", "math.clamp");
            summary.PdaUiRescaleFontApplies =
                CountInvocationInMethod(pdaFile.Root, "ApplyPdaTextScaleVisualSync", "ApplyFontScale") +
                CountTextInMethod(pdaFile.Root, "ApplyFontScale", "text.fontSize");

            if (summary.PdaCorruptedWriterDefinitions != 1 ||
                summary.PdaCorruptedLateFrameCalls != 1 ||
                summary.PdaCorruptedBodySpanWrites < 3)
            {
                findings.Add(new Finding(
                    pdaFile.RelativePath,
                    0,
                    "pda_corrupted_record_route",
                    "PDA missing-lore route must write one corrupted body from LateFrameTick through char spans, writers=" +
                    summary.PdaCorruptedWriterDefinitions +
                    " lateframe_calls=" + summary.PdaCorruptedLateFrameCalls +
                    " span_writes=" + summary.PdaCorruptedBodySpanWrites));
                summary.PhaseFindings++;
            }

            if (summary.PdaRuntimeTmpStringWrites != 0)
            {
                findings.Add(new Finding(
                    pdaFile.RelativePath,
                    0,
                    "pda_runtime_tmp_string_write",
                    "PDA body must use SetCharArray, string/TMP writes=" + summary.PdaRuntimeTmpStringWrites));
                summary.ZeroGcFindings++;
            }

            if (summary.PdaFiniteQualityResolvers != 1 ||
                summary.PdaFiniteQualityCalls < 4 ||
                summary.PdaRawQualitySaturates != 0 ||
                summary.PdaFiniteQualityGuards < 2)
            {
                findings.Add(new Finding(
                    pdaFile.RelativePath,
                    0,
                    "pda_finite_quality_route",
                    "PDA quality route must sanitize continuous GlobalQualityWeight before budget/state/text tokens, resolvers=" +
                    summary.PdaFiniteQualityResolvers +
                    " calls=" + summary.PdaFiniteQualityCalls +
                    " raw_saturates=" + summary.PdaRawQualitySaturates +
                    " guards=" + summary.PdaFiniteQualityGuards));
                summary.PhaseFindings++;
            }

            if (summary.PdaInstantRevealContracts < 4 ||
                summary.PdaInstantRevealLifecycleClears < 2)
            {
                findings.Add(new Finding(
                    pdaFile.RelativePath,
                    0,
                    "pda_instant_reveal_route",
                    "PDA accessibility reveal must queue from public request and flush decoded chars in LateFrameTick, contracts=" +
                    summary.PdaInstantRevealContracts +
                    " lifecycle_clears=" +
                    summary.PdaInstantRevealLifecycleClears));
                summary.PhaseFindings++;
            }

            if (summary.PdaUiRescaleColdInitializers < 2 ||
                summary.PdaUiRescaleLateFrameCalls != 1 ||
                summary.PdaUiRescaleSnapshotReads != 1 ||
                summary.PdaUiRescaleFiniteGuards < 3 ||
                summary.PdaUiRescaleFontApplies < 4)
            {
                findings.Add(new Finding(
                    pdaFile.RelativePath,
                    0,
                    "pda_ui_rescale_route",
                    "PDA text scale must consume unmanaged UIRescaleRequestSignal once in LateFrameTick with finite scale guards and TMP font scalar only, cold_init=" +
                    summary.PdaUiRescaleColdInitializers +
                    " lateframe_calls=" + summary.PdaUiRescaleLateFrameCalls +
                    " snapshot_reads=" + summary.PdaUiRescaleSnapshotReads +
                    " finite_guards=" + summary.PdaUiRescaleFiniteGuards +
                    " font_applies=" + summary.PdaUiRescaleFontApplies));
                summary.PhaseFindings++;
            }
        }

        private static void ScanAccessibilityTextScaleProducerRoute(
            List<FileUnit> files,
            List<Finding> findings,
            ref ApexSummary summary)
        {
            if (!TryFindFile(files, "FontStreamingManager.cs", out FileUnit fontFile))
            {
                findings.Add(new Finding(
                    "FontStreamingManager",
                    0,
                    "ui_rescale_producer_missing",
                    "FontStreamingManager source is absent from Apex scope"));
                summary.DependencyFindings++;
                return;
            }

            if (!TryFindFile(files, "AccessibilitySettings.cs", out FileUnit accessibilityFile))
            {
                findings.Add(new Finding(
                    "AccessibilitySettings",
                    0,
                    "accessibility_text_scale_missing",
                    "AccessibilitySettings source is absent from Apex scope"));
                summary.DependencyFindings++;
                return;
            }

            if (!TryFindFile(files, "SettingsManager.cs", out FileUnit settingsManagerFile))
            {
                findings.Add(new Finding(
                    "SettingsManager",
                    0,
                    "settings_text_scale_missing",
                    "SettingsManager source is absent from Apex scope"));
                summary.DependencyFindings++;
                return;
            }

            if (!TryFindFile(files, "SettingsPanel.cs", out FileUnit settingsPanelFile))
            {
                findings.Add(new Finding(
                    "SettingsPanel",
                    0,
                    "settings_panel_text_scale_missing",
                    "SettingsPanel source is absent from Apex scope"));
                summary.DependencyFindings++;
                return;
            }

            summary.UiRescaleProducerPublicRequests =
                CountTextInFile(fontFile, "public static bool RequestAccessibilityTextScale(float fontScale)");
            summary.UiRescaleProducerReasons =
                CountTextInFile(fontFile, "UIRescaleReasonLocalizedFontSwap = 1") +
                CountTextInFile(fontFile, "UIRescaleReasonAccessibilityTextScale = 2");
            summary.UiRescaleProducerFiniteGuards =
                CountTextInMethod(fontFile.Root, "ResolveSafeTextScale", "math.isfinite(fontScale)") +
                CountTextInMethod(fontFile.Root, "ResolveSafeTextScale", "math.clamp");
            summary.UiRescaleProducerSignalPushes =
                CountTextInMethod(fontFile.Root, "PublishRescaleRequest", "SignalBus<UIRescaleRequestSignal>.TryPushTracked");
            summary.UiRescaleProducerSignalInitializers =
                CountTextInMethod(fontFile.Root, "PublishRescaleRequest", "SignalBus<UIRescaleRequestSignal>.EnsureInitialized()");
            summary.UiRescaleProducerLayoutApplies =
                CountTextInMethod(fontFile.Root, "PublishRescaleRequest", "DiegeticHudManualLayout.ApplyGlobalRescaleRequest(in signal)");
            summary.AccessibilityTextScaleFields =
                CountTextInFile(accessibilityFile, "[Header(\"Text Scale\")]") +
                CountTextInFile(accessibilityFile, "private float textScale") +
                CountTextInFile(accessibilityFile, "private bool _textScaleDirty");
            summary.AccessibilityTextScalePublicSetters =
                CountTextInFile(accessibilityFile, "public void SetTextScale(float scale)");
            summary.AccessibilityTextScaleVisualSyncPublishes =
                CountInvocationInMethod(accessibilityFile.Root, "VisualSyncTick", "PublishTextScaleIfNeededVisualSync") +
                CountTextInMethod(accessibilityFile.Root, "PublishTextScaleIfNeededVisualSync", "FontStreamingManager.RequestAccessibilityTextScale");
            summary.AccessibilityTextScaleFiniteGuards =
                CountTextInMethod(accessibilityFile.Root, "SanitizeTextScale", "math.isfinite(scale)") +
                CountTextInMethod(accessibilityFile.Root, "SanitizeTextScale", "math.clamp") +
                CountTextInMethod(accessibilityFile.Root, "Sanitize01", "math.isfinite(value)");
            summary.SettingsManagerTextScalePersistence =
                CountTextInFile(settingsManagerFile, "private const string TextScaleKey = \"Hecton_TextScale\"") +
                CountTextInFile(settingsManagerFile, "public float TextScale") +
                CountTextInMethod(settingsManagerFile.Root, "LoadAllSettings", "_cachedTextScale = ValidateTextScale") +
                CountTextInMethod(settingsManagerFile.Root, "ResetToDefaults", "TextScale = AccessibilitySettings.DefaultTextScale");
            summary.SettingsManagerTextScaleApplies =
                CountInvocationInMethod(settingsManagerFile.Root, "ApplyAllSettings", "TryApplyAccessibilityTextScale") +
                CountTextInMethod(settingsManagerFile.Root, "TryApplyAccessibilityTextScale", "AccessibilitySettings.TryResolveActiveRuntime") +
                CountTextInMethod(settingsManagerFile.Root, "TryApplyAccessibilityTextScale", "FontStreamingManager.RequestAccessibilityTextScale");
            summary.SettingsManagerTextScaleFiniteGuards =
                CountTextInMethod(settingsManagerFile.Root, "ValidateTextScale", "float.IsNaN(value)") +
                CountTextInMethod(settingsManagerFile.Root, "ValidateTextScale", "float.IsInfinity(value)") +
                CountTextInMethod(settingsManagerFile.Root, "ValidateTextScale", "Mathf.Clamp");
            summary.SettingsPanelTextScaleControls =
                CountTextInFile(settingsPanelFile, "sliderTextScale") +
                CountTextInFile(settingsPanelFile, "txtTextScale") +
                CountTextInFile(settingsPanelFile, "TextScalePercentLabels") +
                CountTextInFile(settingsPanelFile, "autoCreateAccessibilityTextScaleRow");
            summary.SettingsPanelTextScaleBindings =
                CountTextInMethod(settingsPanelFile.Root, "CacheListenerActions", "_textScaleChangedAction = OnTextScaleChanged") +
                CountTextInMethod(settingsPanelFile.Root, "BindSliders", "sliderTextScale.onValueChanged.AddListener") +
                CountTextInMethod(settingsPanelFile.Root, "UnbindSliders", "sliderTextScale.onValueChanged.RemoveListener");
            summary.SettingsPanelTextScalePersistence =
                CountTextInMethod(settingsPanelFile.Root, "LoadCurrentSettings", "_settings.TextScale") +
                CountTextInMethod(settingsPanelFile.Root, "OnApply", "_settings.TextScale = _cachedTextScale") +
                CountTextInMethod(settingsPanelFile.Root, "RefreshAccessibilityUI", "SetValueWithoutNotify");
            summary.SettingsPanelTextScaleZeroGcLabels =
                CountTextInMethod(settingsPanelFile.Root, "RefreshTextScaleValueLabel", "SetCachedLabelIfChanged") +
                CountTextInMethod(settingsPanelFile.Root, "OnTextScaleChanged", "RefreshTextScaleValueLabel") +
                CountTextInMethod(settingsPanelFile.Root, "SanitizeTextScale", "math.isfinite(scale)") +
                CountTextInMethod(settingsPanelFile.Root, "SanitizeTextScale", "math.clamp");
            summary.SettingsPanelTextScaleStringWrites =
                CountTextInMethod(settingsPanelFile.Root, "RefreshTextScaleValueLabel", ".text") +
                CountTextInMethod(settingsPanelFile.Root, "RefreshAccessibilityUI", ".text") +
                CountTextInMethod(settingsPanelFile.Root, "OnTextScaleChanged", ".text") +
                CountTextInMethod(settingsPanelFile.Root, "RefreshTextScaleValueLabel", "SetText(") +
                CountTextInMethod(settingsPanelFile.Root, "RefreshAccessibilityUI", "SetText(") +
                CountTextInMethod(settingsPanelFile.Root, "OnTextScaleChanged", "SetText(");

            if (summary.UiRescaleProducerPublicRequests != 1 ||
                summary.UiRescaleProducerReasons < 2 ||
                summary.UiRescaleProducerFiniteGuards < 2 ||
                summary.UiRescaleProducerSignalPushes != 1 ||
                summary.UiRescaleProducerSignalInitializers != 1 ||
                summary.UiRescaleProducerLayoutApplies != 1)
            {
                findings.Add(new Finding(
                    fontFile.RelativePath,
                    0,
                    "ui_rescale_producer_route",
                    "FontStreamingManager must publish sanitized text-scale requests through UIRescaleRequestSignal and direct layout apply, public_requests=" +
                    summary.UiRescaleProducerPublicRequests +
                    " reasons=" + summary.UiRescaleProducerReasons +
                    " finite_guards=" + summary.UiRescaleProducerFiniteGuards +
                    " signal_pushes=" + summary.UiRescaleProducerSignalPushes +
                    " signal_initializers=" + summary.UiRescaleProducerSignalInitializers +
                    " layout_applies=" + summary.UiRescaleProducerLayoutApplies));
                summary.PhaseFindings++;
            }

            if (summary.AccessibilityTextScaleFields < 3 ||
                summary.AccessibilityTextScalePublicSetters != 1 ||
                summary.AccessibilityTextScaleVisualSyncPublishes < 2 ||
                summary.AccessibilityTextScaleFiniteGuards < 3)
            {
                findings.Add(new Finding(
                    accessibilityFile.RelativePath,
                    0,
                    "accessibility_text_scale_route",
                    "AccessibilitySettings must own a finite continuous text scale and publish it from VisualSyncTick only, fields=" +
                    summary.AccessibilityTextScaleFields +
                    " setters=" + summary.AccessibilityTextScalePublicSetters +
                    " visual_sync_publishes=" + summary.AccessibilityTextScaleVisualSyncPublishes +
                    " finite_guards=" + summary.AccessibilityTextScaleFiniteGuards));
                summary.PhaseFindings++;
            }

            if (summary.SettingsManagerTextScalePersistence < 4 ||
                summary.SettingsManagerTextScaleApplies < 3 ||
                summary.SettingsManagerTextScaleFiniteGuards < 3)
            {
                findings.Add(new Finding(
                    settingsManagerFile.RelativePath,
                    0,
                    "settings_manager_text_scale_route",
                    "SettingsManager must persist, sanitize, and apply accessibility text scale through AccessibilitySettings/FontStreamingManager, persistence=" +
                    summary.SettingsManagerTextScalePersistence +
                    " applies=" + summary.SettingsManagerTextScaleApplies +
                    " finite_guards=" + summary.SettingsManagerTextScaleFiniteGuards));
                summary.PhaseFindings++;
            }

            if (summary.SettingsPanelTextScaleControls < 4 ||
                summary.SettingsPanelTextScaleBindings < 3 ||
                summary.SettingsPanelTextScalePersistence < 3 ||
                summary.SettingsPanelTextScaleZeroGcLabels < 4 ||
                summary.SettingsPanelTextScaleStringWrites != 0)
            {
                findings.Add(new Finding(
                    settingsPanelFile.RelativePath,
                    0,
                    "settings_panel_text_scale_route",
                    "SettingsPanel must expose text scale through cached slider callbacks and SetCharArray labels only, controls=" +
                    summary.SettingsPanelTextScaleControls +
                    " bindings=" + summary.SettingsPanelTextScaleBindings +
                    " persistence=" + summary.SettingsPanelTextScalePersistence +
                    " zero_gc_labels=" + summary.SettingsPanelTextScaleZeroGcLabels +
                    " string_writes=" + summary.SettingsPanelTextScaleStringWrites));
                summary.PhaseFindings++;
            }
        }

        private static void ScanAccessibilityMotionScaleRoute(
            List<FileUnit> files,
            List<Finding> findings,
            ref ApexSummary summary)
        {
            if (!TryFindFile(files, "AccessibilitySettings.cs", out FileUnit accessibilityFile) ||
                !TryFindFile(files, "SettingsManager.cs", out FileUnit settingsManagerFile) ||
                !TryFindFile(files, "SettingsPanel.cs", out FileUnit settingsPanelFile) ||
                !TryFindFile(files, "UIScreenShake.cs", out FileUnit uiShakeFile))
            {
                findings.Add(new Finding(
                    "accessibility_motion_scale",
                    0,
                    "accessibility_motion_scale_missing_source",
                    "Accessibility motion route source set is incomplete"));
                summary.DependencyFindings++;
                return;
            }

            summary.AccessibilityUiMotionFields =
                CountTextInFile(accessibilityFile, "[Header(\"Motion Comfort\")]") +
                CountTextInFile(accessibilityFile, "private float uiMotionScale") +
                CountTextInFile(accessibilityFile, "private bool _uiMotionScaleDirty");
            summary.AccessibilityUiMotionSetters =
                CountTextInFile(accessibilityFile, "public void SetUiMotionScale(float scale)");
            summary.AccessibilityUiMotionVisualSyncPublishes =
                CountInvocationInMethod(accessibilityFile.Root, "VisualSyncTick", "PublishUiMotionScaleIfNeededVisualSync") +
                CountTextInMethod(accessibilityFile.Root, "PublishUiMotionScaleIfNeededVisualSync", "UIScreenShake.SetGlobalMotionScale");
            summary.AccessibilityUiMotionFiniteGuards =
                CountTextInMethod(accessibilityFile.Root, "SanitizeUiMotionScale", "math.isfinite(scale)") +
                CountTextInMethod(accessibilityFile.Root, "SanitizeUiMotionScale", "math.clamp");

            summary.SettingsManagerUiMotionPersistence =
                CountTextInFile(settingsManagerFile, "private const string UiMotionScaleKey = \"Hecton_UiMotionScale\"") +
                CountTextInFile(settingsManagerFile, "public float UiMotionScale") +
                CountTextInMethod(settingsManagerFile.Root, "LoadAllSettings", "_cachedUiMotionScale = ValidateUiMotionScale") +
                CountTextInMethod(settingsManagerFile.Root, "ResetToDefaults", "UiMotionScale = AccessibilitySettings.DefaultUiMotionScale");
            summary.SettingsManagerUiMotionApplies =
                CountInvocationInMethod(settingsManagerFile.Root, "ApplyAllSettings", "TryApplyAccessibilityUiMotionScale") +
                CountTextInMethod(settingsManagerFile.Root, "TryApplyAccessibilityUiMotionScale", "AccessibilitySettings.TryResolveActiveRuntime") +
                CountTextInMethod(settingsManagerFile.Root, "TryApplyAccessibilityUiMotionScale", "UIScreenShake.SetGlobalMotionScale");
            summary.SettingsManagerUiMotionFiniteGuards =
                CountTextInMethod(settingsManagerFile.Root, "ValidateUiMotionScale", "float.IsNaN(value)") +
                CountTextInMethod(settingsManagerFile.Root, "ValidateUiMotionScale", "float.IsInfinity(value)") +
                CountTextInMethod(settingsManagerFile.Root, "ValidateUiMotionScale", "Mathf.Clamp");

            summary.SettingsPanelUiMotionControls =
                CountTextInFile(settingsPanelFile, "sliderUiMotionScale") +
                CountTextInFile(settingsPanelFile, "txtUiMotionScale") +
                CountTextInFile(settingsPanelFile, "autoCreateAccessibilityMotionScaleRow");
            summary.SettingsPanelUiMotionBindings =
                CountTextInMethod(settingsPanelFile.Root, "CacheListenerActions", "_uiMotionScaleChangedAction = OnUiMotionScaleChanged") +
                CountTextInMethod(settingsPanelFile.Root, "BindSliders", "sliderUiMotionScale.onValueChanged.AddListener") +
                CountTextInMethod(settingsPanelFile.Root, "UnbindSliders", "sliderUiMotionScale.onValueChanged.RemoveListener");
            summary.SettingsPanelUiMotionPersistence =
                CountTextInMethod(settingsPanelFile.Root, "LoadCurrentSettings", "_settings.UiMotionScale") +
                CountTextInMethod(settingsPanelFile.Root, "OnApply", "_settings.UiMotionScale = _cachedUiMotionScale") +
                CountTextInMethod(settingsPanelFile.Root, "RefreshAccessibilityUI", "sliderUiMotionScale.SetValueWithoutNotify");
            summary.SettingsPanelUiMotionZeroGcLabels =
                CountTextInMethod(settingsPanelFile.Root, "RefreshUiMotionScaleValueLabel", "SetCachedLabelIfChanged") +
                CountTextInMethod(settingsPanelFile.Root, "OnUiMotionScaleChanged", "RefreshUiMotionScaleValueLabel") +
                CountTextInMethod(settingsPanelFile.Root, "SanitizeUiMotionScale", "math.isfinite(scale)") +
                CountTextInMethod(settingsPanelFile.Root, "SanitizeUiMotionScale", "math.clamp");
            summary.SettingsPanelUiMotionStringWrites =
                CountTextInMethod(settingsPanelFile.Root, "RefreshUiMotionScaleValueLabel", ".text") +
                CountTextInMethod(settingsPanelFile.Root, "RefreshAccessibilityUI", ".text") +
                CountTextInMethod(settingsPanelFile.Root, "OnUiMotionScaleChanged", ".text") +
                CountTextInMethod(settingsPanelFile.Root, "RefreshUiMotionScaleValueLabel", "SetText(") +
                CountTextInMethod(settingsPanelFile.Root, "RefreshAccessibilityUI", "SetText(") +
                CountTextInMethod(settingsPanelFile.Root, "OnUiMotionScaleChanged", "SetText(");

            summary.UiScreenShakeMotionScaleRoute =
                CountTextInFile(uiShakeFile, "public static void SetGlobalMotionScale(float scale)") +
                CountTextInFile(uiShakeFile, "private static float s_globalMotionScale") +
                CountTextInMethod(uiShakeFile.Root, "LateFrameTick", "motionScale") +
                CountTextInMethod(uiShakeFile.Root, "LateFrameTick", "ResetPosition()") +
                CountTextInMethod(uiShakeFile.Root, "BeginShake", "SanitizeMotionScale(s_globalMotionScale)");
            summary.UiScreenShakeMotionFiniteGuards =
                CountTextInMethod(uiShakeFile.Root, "SanitizeMotionScale", "math.isfinite(scale)") +
                CountTextInMethod(uiShakeFile.Root, "SanitizeNonNegativeFinite", "math.isfinite(value)") +
                CountTextInMethod(uiShakeFile.Root, "SanitizePositiveFinite", "math.isfinite(value)");
            summary.UiScreenShakeLateFrameWrites =
                CountTextInMethod(uiShakeFile.Root, "LateFrameTick", "anchoredPosition");

            if (summary.AccessibilityUiMotionFields < 3 ||
                summary.AccessibilityUiMotionSetters != 1 ||
                summary.AccessibilityUiMotionVisualSyncPublishes < 2 ||
                summary.AccessibilityUiMotionFiniteGuards < 2)
            {
                findings.Add(new Finding(
                    accessibilityFile.RelativePath,
                    0,
                    "accessibility_ui_motion_route",
                    "AccessibilitySettings must own finite UI motion scale and publish it from VisualSyncTick only"));
                summary.PhaseFindings++;
            }

            if (summary.SettingsManagerUiMotionPersistence < 4 ||
                summary.SettingsManagerUiMotionApplies < 3 ||
                summary.SettingsManagerUiMotionFiniteGuards < 3)
            {
                findings.Add(new Finding(
                    settingsManagerFile.RelativePath,
                    0,
                    "settings_manager_ui_motion_route",
                    "SettingsManager must persist, sanitize, and apply UI motion scale through AccessibilitySettings/UIScreenShake"));
                summary.PhaseFindings++;
            }

            if (summary.SettingsPanelUiMotionControls < 3 ||
                summary.SettingsPanelUiMotionBindings < 3 ||
                summary.SettingsPanelUiMotionPersistence < 3 ||
                summary.SettingsPanelUiMotionZeroGcLabels < 4 ||
                summary.SettingsPanelUiMotionStringWrites != 0)
            {
                findings.Add(new Finding(
                    settingsPanelFile.RelativePath,
                    0,
                    "settings_panel_ui_motion_route",
                    "SettingsPanel must expose UI motion scale through cached slider callbacks and SetCharArray labels only"));
                summary.PhaseFindings++;
            }

            if (summary.UiScreenShakeMotionScaleRoute < 5 ||
                summary.UiScreenShakeMotionFiniteGuards < 3 ||
                summary.UiScreenShakeLateFrameWrites != 1)
            {
                findings.Add(new Finding(
                    uiShakeFile.RelativePath,
                    0,
                    "ui_screen_shake_motion_route",
                    "UIScreenShake must consume motion scale in LateFrameTick and keep finite guards on scalar motion"));
                summary.PhaseFindings++;
            }
        }

        private static void ScanUiRescaleBroadcastRoute(
            List<FileUnit> files,
            List<Finding> findings,
            ref ApexSummary summary)
        {
            FileUnit layoutFile;
            if (!TryFindFile(files, "DiegeticHudManualLayout.cs", out layoutFile))
            {
                findings.Add(new Finding(
                    "DiegeticHudManualLayout",
                    0,
                    "ui_rescale_layout_route_missing",
                    "DiegeticHudManualLayout source is absent from Apex scope"));
                summary.DependencyFindings++;
                return;
            }

            summary.UiRescaleLayoutSnapshotReads =
                CountInvocationTextInMethod(layoutFile.Root, "FlushGlobalRescaleRequests", "SignalBus<UIRescaleRequestSignal>.GetFrameSnapshot");
            summary.UiRescaleLayoutLegacyConsumes =
                CountTextInMethod(layoutFile.Root, "FlushGlobalRescaleRequests", "TryConsumeFrame");
            summary.UiRescaleLayoutDedupFields =
                CountTextInFile(layoutFile, "private static uint s_lastRescaleFrame") +
                CountTextInFile(layoutFile, "private static uint s_lastRescaleSourceHash") +
                CountTextInFile(layoutFile, "private static uint s_lastRescaleFontScaleBits") +
                CountTextInFile(layoutFile, "private static ushort s_lastRescaleReason");
            summary.UiRescaleLayoutResetClears =
                CountTextInMethod(layoutFile.Root, "ResetStaticState", "s_lastRescaleFrame = 0u") +
                CountTextInMethod(layoutFile.Root, "ResetStaticState", "s_lastRescaleSourceHash = 0u") +
                CountTextInMethod(layoutFile.Root, "ResetStaticState", "s_lastRescaleFontScaleBits = 0u") +
                CountTextInMethod(layoutFile.Root, "ResetStaticState", "s_lastRescaleReason = 0");
            summary.UiRescaleLayoutRebuildCalls =
                CountInvocationInMethod(layoutFile.Root, "FlushGlobalRescaleRequests", "RebuildRegisteredLayouts") +
                CountInvocationInMethod(layoutFile.Root, "ApplyGlobalRescaleRequest", "RebuildRegisteredLayouts") +
                CountInvocationInMethod(layoutFile.Root, "RebuildRegisteredLayouts", "RebuildLayout");

            if (summary.UiRescaleLayoutSnapshotReads != 1 ||
                summary.UiRescaleLayoutLegacyConsumes != 0 ||
                summary.UiRescaleLayoutDedupFields < 4 ||
                summary.UiRescaleLayoutResetClears < 4 ||
                summary.UiRescaleLayoutRebuildCalls < 1)
            {
                findings.Add(new Finding(
                    layoutFile.RelativePath,
                    0,
                    "ui_rescale_layout_broadcast_route",
                    "Diegetic HUD rescale must read the broadcast snapshot without destructive TryConsumeFrame, snapshot_reads=" +
                    summary.UiRescaleLayoutSnapshotReads +
                    " legacy_consumes=" + summary.UiRescaleLayoutLegacyConsumes +
                    " dedup_fields=" + summary.UiRescaleLayoutDedupFields +
                    " reset_clears=" + summary.UiRescaleLayoutResetClears +
                    " rebuild_calls=" + summary.UiRescaleLayoutRebuildCalls));
                summary.PhaseFindings++;
            }
        }

        private static void ScanAudioLogGlitchRoute(
            string projectRoot,
            List<FileUnit> files,
            List<Finding> findings,
            ref ApexSummary summary)
        {
            FileUnit audioEventsFile;
            FileUnit audioSystemFile;
            bool hasAudioEvents = TryFindFile(files, "AudioLogEvents.cs", out audioEventsFile) ||
                                  TryLoadAuditFile(projectRoot, "Assets/_Project/Scripts/AudioLog/AudioLogEvents.cs", findings, out audioEventsFile);
            bool hasAudioSystem = TryFindFile(files, "AudioLogSystem.cs", out audioSystemFile) ||
                                  TryLoadAuditFile(projectRoot, "Assets/_Project/Scripts/AudioLog/AudioLogSystem.cs", findings, out audioSystemFile);
            if (!hasAudioEvents || !hasAudioSystem)
            {
                findings.Add(new Finding(
                    "AudioLogGlitchRoute",
                    0,
                    "audio_glitch_route_sources_missing",
                    "events=" + hasAudioEvents + " system=" + hasAudioSystem));
                summary.DependencyFindings++;
                return;
            }

            string eventsSource = audioEventsFile.Root.ToFullString();
            summary.AudioGlitchDtoDefinitions =
                eventsSource.IndexOf("public struct " + AudioGlitchParametersDtoName, StringComparison.Ordinal) >= 0 &&
                eventsSource.IndexOf("StructLayout(LayoutKind.Explicit, Size = 8)", StringComparison.Ordinal) >= 0 &&
                eventsSource.IndexOf("[FieldOffset(24)] public " + AudioGlitchParametersDtoName + " Glitch", StringComparison.Ordinal) >= 0
                    ? 1
                    : 0;
            summary.AudioGlitchPlaybackOverloads =
                CountTextInFile(audioEventsFile, "in " + AudioGlitchParametersDtoName + " glitch");
            summary.AudioGlitchSanitizers =
                CountTextInFile(audioEventsFile, "public static AudioGlitchParametersDTO Sanitize") +
                CountTextInFile(audioEventsFile, "KnownFlagMask");
            summary.AudioGlitchEnqueueSanitizeCalls =
                CountTextInFile(audioEventsFile, "Glitch = AudioGlitchParametersDTO.Sanitize(in glitch)");
            summary.AudioGlitchDurationGuards =
                CountTextInFile(audioEventsFile, "SanitizeDurationSeconds(durationSeconds)") +
                CountTextInFile(audioEventsFile, "float.IsNaN(durationSeconds)") +
                CountTextInMethod(audioSystemFile.Root, "PlayLogByHash", "ResolvePlaybackDuration(data.Duration)") +
                CountTextInMethod(audioSystemFile.Root, "PlayEncryptedPartialPreview", "ResolvePlaybackDuration(data.Duration)") +
                CountTextInMethod(audioSystemFile.Root, "NotifyAtmosphericWarningStarted", "ResolvePlaybackDuration(durationSeconds)");
            if (summary.AudioGlitchDtoDefinitions != 1 ||
                summary.AudioGlitchPlaybackOverloads <= 0 ||
                summary.AudioGlitchSanitizers < 2 ||
                summary.AudioGlitchEnqueueSanitizeCalls != 1 ||
                summary.AudioGlitchDurationGuards < 5)
            {
                findings.Add(new Finding(
                    audioEventsFile.RelativePath,
                    0,
                    "audio_glitch_payload_contract",
                    "Audio logs must carry an 8-byte explicit DTO inside the event payload, dto_defs=" +
                    summary.AudioGlitchDtoDefinitions +
                    " overload_refs=" + summary.AudioGlitchPlaybackOverloads +
                    " sanitizers=" + summary.AudioGlitchSanitizers +
                    " enqueue_sanitize_calls=" + summary.AudioGlitchEnqueueSanitizeCalls +
                    " duration_guards=" + summary.AudioGlitchDurationGuards));
                summary.DependencyFindings++;
            }

            summary.AudioGlitchResolveMethods =
                CountTextInFile(audioSystemFile, "private AudioGlitchParametersDTO ResolveAudioGlitchParameters");
            summary.AudioGlitchQualityWeightReads =
                CountTextInMethod(audioSystemFile.Root, "ResolveAudioGlitchParameters", "HomeostasisBrain.GlobalQualityWeight");
            summary.AudioGlitchFiniteGuards =
                CountTextInMethod(audioSystemFile.Root, "ResolveAudioGlitchParameters", "Sanitize01(ResolveNarrativeRadioInterference01())") +
                CountTextInMethod(audioSystemFile.Root, "ResolveAudioGlitchParameters", "Sanitize01(HomeostasisBrain.GlobalQualityWeight)") +
                CountTextInMethod(audioSystemFile.Root, "Unit01ToPermille", "!math.isfinite(value)") +
                CountTextInMethod(audioSystemFile.Root, "ResolveNarrativeRadioInterference01", "math.isfinite(rawDepthMeters)") +
                CountTextInMethod(audioSystemFile.Root, "ResolveNarrativeRadioInterference01", "Sanitize01(traumaDispatcher.HazardRadiationSignal01)");
            summary.AudioGlitchLateFrameFlushes =
                CountInvocationInMethod(audioSystemFile.Root, "LateFrameTick", "FlushPendingPlaybackVisualSync");
            summary.AudioGlitchPendingDtoTransfers =
                CountTextInMethod(audioSystemFile.Root, "QueuePlaybackVisualSync", "_pendingPlaybackGlitch = safeGlitch") +
                CountTextInMethod(audioSystemFile.Root, "FlushPendingPlaybackVisualSync", "AudioGlitchParametersDTO glitch = _pendingPlaybackGlitch") +
                CountTextInMethod(audioSystemFile.Root, "ClearPendingPlaybackSync", "_pendingPlaybackGlitch = default");
            summary.AudioGlitchPlaybackStarts =
                CountTextInMethod(audioSystemFile.Root, "PlayLogByHash", "TryRaisePlaybackStarted(_currentLogHash, _playbackTimer, in glitch, data)") +
                CountTextInMethod(audioSystemFile.Root, "PlayEncryptedPartialPreview", "TryRaisePlaybackStarted(_currentLogHash, _playbackTimer, in glitch, data)");
            summary.AudioGlitchVisualSyncCalls =
                CountInvocationTextInMethod(audioSystemFile.Root, "FlushPendingPlaybackVisualSync", "SetNarrativeRadioInterference") +
                CountInvocationTextInMethod(audioSystemFile.Root, "FlushPendingPlaybackVisualSync", "TryPlayStatic2DBitCrushed") +
                CountInvocationTextInMethod(audioSystemFile.Root, "FlushPendingPlaybackVisualSync", "PlayStatic2D");
            summary.AudioGlitchStopCancelsPending =
                CountTextInMethod(audioSystemFile.Root, "StopPlayback", "ClearPendingPlaybackSync()") +
                CountTextInMethod(audioSystemFile.Root, "StopPlayback", "TryUnregisterLateFrame()");
            summary.AudioGlitchPlaybackStateWrites =
                CountTextInMethod(audioSystemFile.Root, "PlayLogByHash", "_currentPlaybackBitCrushed = bitCrushRouteActive");

            if (summary.AudioGlitchResolveMethods != 1 ||
                summary.AudioGlitchQualityWeightReads != 1 ||
                summary.AudioGlitchFiniteGuards < 5 ||
                summary.AudioGlitchLateFrameFlushes != 1 ||
                summary.AudioGlitchPendingDtoTransfers < 3 ||
                summary.AudioGlitchPlaybackStarts != 2 ||
                summary.AudioGlitchVisualSyncCalls < 3 ||
                summary.AudioGlitchStopCancelsPending < 2 ||
                summary.AudioGlitchPlaybackStateWrites != 1)
            {
                findings.Add(new Finding(
                    audioSystemFile.RelativePath,
                    0,
                    "audio_glitch_phase_route",
                    "Audio glitch route must resolve continuous quality data, queue DTO state, and flush presentation in LateFrameTick; resolve=" +
                    summary.AudioGlitchResolveMethods +
                    " quality_reads=" + summary.AudioGlitchQualityWeightReads +
                    " finite_guards=" + summary.AudioGlitchFiniteGuards +
                    " lateframe_flushes=" + summary.AudioGlitchLateFrameFlushes +
                    " dto_transfers=" + summary.AudioGlitchPendingDtoTransfers +
                    " starts=" + summary.AudioGlitchPlaybackStarts +
                    " visual_sync_calls=" + summary.AudioGlitchVisualSyncCalls +
                    " stop_cancels_pending=" + summary.AudioGlitchStopCancelsPending +
                    " playback_state_writes=" + summary.AudioGlitchPlaybackStateWrites));
                summary.PhaseFindings++;
            }
        }

        private static void ScanSubtitleAudioLogPhaseBridge(
            List<FileUnit> files,
            List<Finding> findings,
            ref ApexSummary summary)
        {
            if (!TryFindFile(files, "SubtitleManager.cs", out FileUnit subtitleFile))
            {
                findings.Add(new Finding(
                    "SubtitleManager",
                    0,
                    "subtitle_audio_log_phase_source_missing",
                    "SubtitleManager source is absent from Apex scope"));
                summary.DependencyFindings++;
                return;
            }

            summary.SubtitleAudioLogPendingRingDefinitions =
                CountTextInFile(subtitleFile, "private struct PendingAudioLogSubtitleEvent") +
                CountTextInFile(subtitleFile, "private readonly PendingAudioLogSubtitleEvent[] _pendingAudioLogEvents") +
                CountTextInFile(subtitleFile, "private int _pendingAudioLogEventHead") +
                CountTextInFile(subtitleFile, "private int _pendingAudioLogEventCount");
            summary.SubtitleAudioLogCallbackQueues =
                CountInvocationInMethod(subtitleFile.Root, "OnAudioLogEvent", "QueueAudioLogSubtitleEvent");
            summary.SubtitleAudioLogCallbackDirectPresentationCalls =
                CountInvocationInMethod(subtitleFile.Root, "OnAudioLogEvent", "HandleAudioLogPlaybackStarted") +
                CountInvocationInMethod(subtitleFile.Root, "OnAudioLogEvent", "HandleAudioLogPlaybackEnded") +
                CountInvocationInMethod(subtitleFile.Root, "OnAudioLogEvent", "ApplySubtitleBuffer") +
                CountInvocationInMethod(subtitleFile.Root, "OnAudioLogEvent", "NotifyCueChanged") +
                CountInvocationInMethod(subtitleFile.Root, "OnAudioLogEvent", "EmitAudioLogCueSensoryPulse");
            summary.SubtitleAudioLogLateFrameDrains =
                CountInvocationInMethod(subtitleFile.Root, "LateFrameTick", "DrainPendingAudioLogEventsVisualSync");
            summary.SubtitleAudioLogVisualSyncDispatches =
                CountInvocationInMethod(subtitleFile.Root, "DrainPendingAudioLogEventsVisualSync", "HandleAudioLogPlaybackStarted") +
                CountInvocationInMethod(subtitleFile.Root, "DrainPendingAudioLogEventsVisualSync", "HandleAudioLogPlaybackEnded");
            summary.SubtitleAudioLogLifecycleClears =
                CountInvocationInMethod(subtitleFile.Root, "OnDisable", "ClearPendingAudioLogSubtitleEvents") +
                CountInvocationInMethod(subtitleFile.Root, "OnDestroy", "ClearPendingAudioLogSubtitleEvents") +
                CountInvocationInMethod(subtitleFile.Root, "OnDisable", "ClearTimedAudioLogState") +
                CountInvocationInMethod(subtitleFile.Root, "OnDestroy", "ClearTimedAudioLogState");
            summary.SubtitleAudioLogDurationGuards =
                CountTextInMethod(subtitleFile.Root, "SanitizeAudioLogEventDuration", "math.isfinite(durationSeconds)") +
                CountTextInMethod(subtitleFile.Root, "SanitizeAudioLogEventDuration", "math.min(durationSeconds, 86400f)");

            if (summary.SubtitleAudioLogPendingRingDefinitions < 4 ||
                summary.SubtitleAudioLogCallbackQueues < 2 ||
                summary.SubtitleAudioLogCallbackDirectPresentationCalls != 0 ||
                summary.SubtitleAudioLogLateFrameDrains != 1 ||
                summary.SubtitleAudioLogVisualSyncDispatches < 2 ||
                summary.SubtitleAudioLogLifecycleClears < 4 ||
                summary.SubtitleAudioLogDurationGuards < 2)
            {
                findings.Add(new Finding(
                    subtitleFile.RelativePath,
                    0,
                    "subtitle_audio_log_phase_bridge",
                    "Audio-log subtitle callbacks must queue value-only events and dispatch presentation from LateFrameTick, ring_defs=" +
                    summary.SubtitleAudioLogPendingRingDefinitions +
                    " queues=" + summary.SubtitleAudioLogCallbackQueues +
                    " direct_callback_calls=" + summary.SubtitleAudioLogCallbackDirectPresentationCalls +
                    " lateframe_drains=" + summary.SubtitleAudioLogLateFrameDrains +
                    " visual_dispatches=" + summary.SubtitleAudioLogVisualSyncDispatches +
                    " lifecycle_clears=" + summary.SubtitleAudioLogLifecycleClears +
                    " duration_guards=" + summary.SubtitleAudioLogDurationGuards));
                summary.PhaseFindings++;
            }
        }

        private static void ScanTerminalOsBlackBoxDumpRoute(
            List<FileUnit> files,
            List<Finding> findings,
            ref ApexSummary summary)
        {
            FileUnit terminalOsFile;
            if (!TryFindFile(files, "TerminalOsRuntime.cs", out terminalOsFile))
            {
                findings.Add(new Finding(
                    "TerminalOsRuntime",
                    0,
                    "terminal_os_dump_route_missing",
                    "TerminalOsRuntime source is absent from Apex scope"));
                summary.DependencyFindings++;
                return;
            }

            string source = terminalOsFile.Root.ToFullString();
            summary.TerminalOsDumpThreadPrimitives =
                CountOccurrences(source, "AutoResetEvent") +
                CountOccurrences(source, "new Thread") +
                CountOccurrences(source, "WriterLoop(");
            if (summary.TerminalOsDumpThreadPrimitives != 0)
            {
                findings.Add(new Finding(
                    terminalOsFile.RelativePath,
                    0,
                    "terminal_os_dump_orphan_thread_risk",
                    "Decryption dump writer must be synchronous fault-path drain, thread primitives=" + summary.TerminalOsDumpThreadPrimitives));
                summary.DependencyFindings++;
            }

            summary.TerminalOsDumpSynchronousDrains = CountInvocationTextInMethod(
                terminalOsFile.Root,
                "TryEnqueue",
                "DrainPending");
            summary.TerminalOsDumpDrainReturnRoutes = CountTextInMethod(
                terminalOsFile.Root,
                "TryEnqueue",
                "return DrainPending();");
            summary.TerminalOsDumpBooleanDrains = CountOccurrences(source, "private bool DrainPending()");
            summary.TerminalOsDumpBooleanWrites = CountOccurrences(source, "private unsafe bool WritePendingUnsafe(");
            summary.TerminalOsDumpContextWarnings = CountTextInMethod(
                terminalOsFile.Root,
                "TryDumpDecryptionBlackBox",
                "GlobalTelemetryBus.PublishPerformanceWarning(DecryptionDumpBackpressureHash, DecryptionDumpContextHash, 1f)");
            summary.TerminalOsDumpGateLockScopes = CountLockStatementsInMethod(
                terminalOsFile.Root,
                "DrainPending");
            summary.TerminalOsDumpWritesAfterGateLock = CountInvocationTextInMethodOutsideLock(
                terminalOsFile.Root,
                "DrainPending",
                "WritePendingUnsafe");
            if (summary.TerminalOsDumpSynchronousDrains != 1)
            {
                findings.Add(new Finding(
                    terminalOsFile.RelativePath,
                    0,
                    "terminal_os_dump_missing_synchronous_drain",
                    "TryEnqueue must synchronously drain exactly once, calls=" + summary.TerminalOsDumpSynchronousDrains));
                summary.PhaseFindings++;
            }

            if (summary.TerminalOsDumpDrainReturnRoutes != 1 ||
                summary.TerminalOsDumpBooleanDrains != 1 ||
                summary.TerminalOsDumpBooleanWrites != 1)
            {
                findings.Add(new Finding(
                    terminalOsFile.RelativePath,
                    0,
                    "terminal_os_dump_false_success_risk",
                    "TryEnqueue must return the synchronous dump write result, return_drains=" +
                    summary.TerminalOsDumpDrainReturnRoutes +
                    " bool_drains=" + summary.TerminalOsDumpBooleanDrains +
                    " bool_writes=" + summary.TerminalOsDumpBooleanWrites));
                summary.PhaseFindings++;
            }

            if (summary.TerminalOsDumpContextWarnings != 1 ||
                CountOccurrences(source, "private const uint DecryptionDumpContextHash") != 1)
            {
                findings.Add(new Finding(
                    terminalOsFile.RelativePath,
                    0,
                    "terminal_os_dump_warning_context_drift",
                    "Decryption dump backpressure warning must use its own fixed context hash, context_warnings=" +
                    summary.TerminalOsDumpContextWarnings));
                summary.DependencyFindings++;
            }

            if (summary.TerminalOsDumpGateLockScopes != 1 ||
                summary.TerminalOsDumpWritesAfterGateLock != 1)
            {
                findings.Add(new Finding(
                    terminalOsFile.RelativePath,
                    0,
                    "terminal_os_dump_gate_lock_drift",
                    "DrainPending must copy under one gate lock and call WritePendingUnsafe after release, gate_locks=" +
                    summary.TerminalOsDumpGateLockScopes +
                    " writes_after_lock=" + summary.TerminalOsDumpWritesAfterGateLock));
                summary.LockFindings++;
            }
        }

        private static void ScanTerminalOsSceneBinding(
            string projectRoot,
            List<Finding> findings,
            ref ApexSummary summary)
        {
            string scenePath = Path.Combine(projectRoot, TerminalOsScenePath.Replace('/', Path.DirectorySeparatorChar));
            string planPath = Path.Combine(projectRoot, TerminalOsScenePlacementPlanPath.Replace('/', Path.DirectorySeparatorChar));
            int expectedTerminals = CountTerminalRowsInPlacementPlan(planPath, findings, ref summary);
            summary.TerminalOsExpectedTerminals = expectedTerminals;
            if (expectedTerminals <= 0)
                return;

            string sceneText;
            try
            {
                sceneText = File.ReadAllText(scenePath, Encoding.UTF8);
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is NotSupportedException)
            {
                findings.Add(new Finding(
                    TerminalOsScenePath,
                    0,
                    "terminal_os_scene_read",
                    exception.GetType().Name));
                summary.DependencyFindings++;
                return;
            }

            ScanWorldSceneCore(sceneText, findings, ref summary);

            bool hasRuntimeObject = sceneText.IndexOf(TerminalOsRuntimeObjectName, StringComparison.Ordinal) >= 0;
            bool hasRuntimeScript = sceneText.IndexOf(TerminalOsRuntimeScriptGuid, StringComparison.OrdinalIgnoreCase) >= 0;
            if (!hasRuntimeObject && !hasRuntimeScript)
            {
                summary.TerminalOsSceneBindingWarnings++;
                return;
            }

            if (!hasRuntimeObject || !hasRuntimeScript)
            {
                findings.Add(new Finding(
                    TerminalOsScenePath,
                    0,
                    "terminal_os_runtime_missing",
                    "Scene-owned TerminalOsRuntime object/script is missing"));
                summary.DependencyFindings++;
                return;
            }

            summary.TerminalOsRuntimeRows = 1;
            string runtimeBlock = ExtractTerminalOsRuntimeBlock(sceneText);
            int rendererSlots = CountSerializedReferenceArray(runtimeBlock, "terminalRenderers", TerminalOsScenePath, findings, ref summary);
            int transformSlots = CountSerializedReferenceArray(runtimeBlock, "terminalTransforms", TerminalOsScenePath, findings, ref summary);
            summary.TerminalOsRendererSlots = rendererSlots;
            summary.TerminalOsTransformSlots = transformSlots;
            ScanTerminalOsPreviewHashPairs(sceneText, expectedTerminals, findings, ref summary);
            if (rendererSlots != expectedTerminals)
            {
                findings.Add(new Finding(
                    TerminalOsScenePath,
                    0,
                    "terminal_os_renderer_slots",
                    "expected=" + expectedTerminals + " actual=" + rendererSlots));
                summary.DependencyFindings++;
            }

            if (transformSlots != expectedTerminals)
            {
                findings.Add(new Finding(
                    TerminalOsScenePath,
                    0,
                    "terminal_os_transform_slots",
                    "expected=" + expectedTerminals + " actual=" + transformSlots));
                summary.DependencyFindings++;
            }

            if (rendererSlots == expectedTerminals && transformSlots == expectedTerminals)
                summary.TerminalOsVerifiedSlots = expectedTerminals;
        }

        private static void ScanTerminalOsPreviewHashPairs(
            string sceneText,
            int expectedTerminals,
            List<Finding> findings,
            ref ApexSummary summary)
        {
            MatchCollection matches = Regex.Matches(
                sceneText,
                @"(?m)^\s+propertyPath:\s+terminalOsPreviewHash\s*\r?\n" +
                @"^\s+value:\s+(?<hash>\d+)\s*\r?\n" +
                @"^\s+objectReference:[^\r\n]*\r?\n" +
                @"^\s+-\s+target:[^\r\n]*\r?\n" +
                @"^\s+propertyPath:\s+terminalOsPreviewIndex\s*\r?\n" +
                @"^\s+value:\s+(?<index>-?\d+)\s*$");

            summary.TerminalOsPreviewHashPairs = matches.Count;
            if (matches.Count != expectedTerminals)
            {
                findings.Add(new Finding(
                    TerminalOsScenePath,
                    0,
                    "terminal_os_preview_hash_pairs",
                    "expected=" + expectedTerminals + " actual=" + matches.Count));
                summary.DependencyFindings++;
            }

            bool[] seen = new bool[Math.Max(expectedTerminals, 0)];
            for (int i = 0; i < matches.Count; i++)
            {
                Match match = matches[i];
                if (!uint.TryParse(match.Groups["hash"].Value, out uint hash) ||
                    !int.TryParse(match.Groups["index"].Value, out int index))
                {
                    findings.Add(new Finding(
                        TerminalOsScenePath,
                        0,
                        "terminal_os_preview_hash_parse",
                        "pair=" + i));
                    summary.DependencyFindings++;
                    continue;
                }

                if (index < 0 || index >= expectedTerminals)
                {
                    findings.Add(new Finding(
                        TerminalOsScenePath,
                        0,
                        "terminal_os_preview_index_range",
                        "index=" + index + " expected_range=0.." + (expectedTerminals - 1)));
                    summary.DependencyFindings++;
                    continue;
                }

                if (seen[index])
                {
                    summary.TerminalOsPreviewHashDuplicateIndices++;
                    findings.Add(new Finding(
                        TerminalOsScenePath,
                        0,
                        "terminal_os_preview_index_duplicate",
                        "index=" + index));
                    summary.DependencyFindings++;
                    continue;
                }

                seen[index] = true;
                uint expectedHash = ComputeTerminalOsHash(index);
                if (hash != expectedHash)
                {
                    summary.TerminalOsPreviewHashMismatches++;
                    findings.Add(new Finding(
                        TerminalOsScenePath,
                        0,
                        "terminal_os_preview_hash_mismatch",
                        "index=" + index + " expected=" + expectedHash + " actual=" + hash));
                    summary.DependencyFindings++;
                }
            }
        }

        private static uint ComputeTerminalOsHash(int index)
        {
            unchecked
            {
                uint hash = 0x5445524Du;
                hash = (hash ^ (uint)(index + 1)) * 16777619u;
                hash = (hash ^ ((uint)index << 16)) * 16777619u;
                return hash == 0u ? 1u : hash;
            }
        }

        private static void ScanWorldSceneCore(
            string sceneText,
            List<Finding> findings,
            ref ApexSummary summary)
        {
            summary.SceneWorldBytes = Encoding.UTF8.GetByteCount(sceneText);
            summary.SceneWorldRoots = CountSceneRoots(sceneText);
            summary.SceneWorldMapMagicRows = CountString(sceneText, WorldSceneMapMagicMarker, StringComparison.Ordinal);
            summary.SceneWorldTerrainRows = Regex.Matches(sceneText, @"(?m)^Terrain:\s*$").Count;
            summary.SceneWorldTerrainColliderRows = Regex.Matches(sceneText, @"(?m)^TerrainCollider:\s*$").Count;
            summary.SceneWorldCrestMarkers = CountString(sceneText, WorldSceneCrestMarker, StringComparison.Ordinal);
            CountWorldSceneOceanPrefabRefs(sceneText, ref summary);

            if (summary.SceneWorldRoots <= 0)
            {
                findings.Add(new Finding(
                    TerminalOsScenePath,
                    0,
                    "world_scene_roots",
                    "World scene root list missing or empty"));
                summary.DependencyFindings++;
            }

            if (summary.SceneWorldMapMagicRows <= 0)
            {
                summary.SceneWorldDependencyWarnings++;
            }

            if (summary.SceneWorldTerrainRows <= 0)
            {
                summary.SceneWorldDependencyWarnings++;
            }

            if (summary.SceneWorldTerrainColliderRows <= 0)
            {
                summary.SceneWorldDependencyWarnings++;
            }
        }

        private static void ScanRuntimeStructLayoutProof(
            List<FileUnit> files,
            List<Finding> findings,
            ref ApexSummary summary)
        {
            summary.RuntimeStructSizeofReferences = CountTextInFiles(files, "UnsafeUtility.SizeOf<");
            for (int i = 0; i < files.Count; i++)
            {
                using (IEnumerator<StructDeclarationSyntax> structs =
                    files[i].Root.DescendantNodes().OfType<StructDeclarationSyntax>().GetEnumerator())
                {
                    while (structs.MoveNext())
                    {
                        StructDeclarationSyntax declaration = structs.Current;
                        AttributeSyntax layout = FindStructLayoutAttribute(declaration);
                        if (layout == null)
                            continue;

                        summary.RuntimeStructLayoutsChecked++;
                        string layoutText = layout.ToString();
                        if (layoutText.IndexOf("Pack=1", StringComparison.Ordinal) >= 0 ||
                            layoutText.IndexOf("Pack = 1", StringComparison.Ordinal) >= 0)
                        {
                            findings.Add(new Finding(
                                RelativePath(layout),
                                LineOf(layout),
                                "runtime_struct_pack_one",
                                declaration.Identifier.ValueText + " uses Pack=1 in a runtime-view struct"));
                            summary.RuntimeStructPackOneFindings++;
                        }

                        if (!TryGetStructLayoutLiteralSize(layout, out int byteSize))
                            continue;

                        if ((byteSize & 7) == 0)
                        {
                            summary.RuntimeStructLiteralSizeAligned++;
                            continue;
                        }

                        findings.Add(new Finding(
                            RelativePath(layout),
                            LineOf(layout),
                            "runtime_struct_size_unaligned",
                            declaration.Identifier.ValueText + " size " + byteSize + " is not 8-byte aligned"));
                        summary.RuntimeStructLiteralSizeUnaligned++;
                    }
                }
            }
        }

        private static void ScanProjectAssetMetaIntegrity(
            string projectRoot,
            List<Finding> findings,
            ref ApexSummary summary)
        {
            string assetsRoot = Path.Combine(projectRoot, "Assets");
            if (!Directory.Exists(assetsRoot))
                return;

            try
            {
                foreach (string metaPath in Directory.EnumerateFiles(assetsRoot, "*.meta", SearchOption.AllDirectories))
                {
                    summary.MetaFilesScanned++;
                    string targetPath = metaPath.Substring(0, metaPath.Length - ".meta".Length);
                    if (File.Exists(targetPath) || Directory.Exists(targetPath))
                        continue;

                    findings.Add(new Finding(
                        ToProjectRelativePath(projectRoot, metaPath),
                        0,
                        "orphan_meta_file",
                        "Meta file has no matching asset or directory"));
                    summary.OrphanMetaFiles++;
                }
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is NotSupportedException)
            {
                findings.Add(new Finding("Assets", 0, "meta_scan_failed", exception.GetType().Name));
                summary.DependencyFindings++;
                return;
            }

            ScanRequiredSourceMetas(projectRoot, assetsRoot, findings, ref summary);
        }

        private static void CountWorldSceneOceanPrefabRefs(string sceneText, ref ApexSummary summary)
        {
            string projectRoot = ResolveProjectRoot();
            for (int i = 0; i < WorldSceneOceanPrefabPaths.Length; i++)
            {
                string prefabPath = Path.Combine(
                    projectRoot,
                    WorldSceneOceanPrefabPaths[i].Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(prefabPath))
                    continue;

                summary.SceneWorldOceanPrefabAssets++;
                string guid = ReadUnityMetaGuid(prefabPath + ".meta");
                if (string.IsNullOrEmpty(guid))
                    continue;

                summary.SceneWorldOceanPrefabRefs += CountString(sceneText, guid, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static void ScanRequiredSourceMetas(
            string projectRoot,
            string assetsRoot,
            List<Finding> findings,
            ref ApexSummary summary)
        {
            try
            {
                foreach (string path in Directory.EnumerateFiles(assetsRoot, "*", SearchOption.AllDirectories))
                {
                    if (Path.GetExtension(path).Equals(".meta", StringComparison.OrdinalIgnoreCase) ||
                        !RequiresUnityMeta(path))
                    {
                        continue;
                    }

                    summary.SourceFilesRequiringMetaScanned++;
                    if (File.Exists(path + ".meta"))
                        continue;

                    findings.Add(new Finding(
                        ToProjectRelativePath(projectRoot, path),
                        0,
                        "source_meta_missing",
                        Path.GetExtension(path) + " file has no matching .meta"));
                    summary.MissingSourceMetaFiles++;
                }
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is NotSupportedException)
            {
                findings.Add(new Finding("Assets", 0, "source_meta_scan_failed", exception.GetType().Name));
                summary.DependencyFindings++;
                return;
            }
        }

        private static bool RequiresUnityMeta(string path)
        {
            string extension = Path.GetExtension(path);
            for (int i = 0; i < SourceMetaRequiredExtensions.Length; i++)
            {
                if (string.Equals(extension, SourceMetaRequiredExtensions[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static AttributeSyntax FindStructLayoutAttribute(StructDeclarationSyntax declaration)
        {
            if (declaration == null)
                return null;

            SyntaxList<AttributeListSyntax> lists = declaration.AttributeLists;
            for (int i = 0; i < lists.Count; i++)
            {
                SeparatedSyntaxList<AttributeSyntax> attributes = lists[i].Attributes;
                for (int j = 0; j < attributes.Count; j++)
                {
                    AttributeSyntax attribute = attributes[j];
                    string name = attribute.Name.ToString();
                    if (name.EndsWith("StructLayout", StringComparison.Ordinal) ||
                        name.EndsWith("StructLayoutAttribute", StringComparison.Ordinal))
                    {
                        return attribute;
                    }
                }
            }

            return null;
        }

        private static bool TryGetStructLayoutLiteralSize(AttributeSyntax layout, out int byteSize)
        {
            byteSize = 0;
            if (layout == null || layout.ArgumentList == null)
                return false;

            SeparatedSyntaxList<AttributeArgumentSyntax> arguments = layout.ArgumentList.Arguments;
            for (int i = 0; i < arguments.Count; i++)
            {
                AttributeArgumentSyntax argument = arguments[i];
                if (argument.NameEquals == null ||
                    !string.Equals(argument.NameEquals.Name.Identifier.ValueText, "Size", StringComparison.Ordinal))
                {
                    continue;
                }

                if (argument.Expression is LiteralExpressionSyntax literal &&
                    literal.Token.Value is int value)
                {
                    byteSize = value;
                    return byteSize > 0;
                }

                Match match = Regex.Match(argument.Expression.ToString(), @"^\s*(?<size>\d+)\s*$");
                if (!match.Success)
                    return false;

                byteSize = int.Parse(match.Groups["size"].Value);
                return byteSize > 0;
            }

            return false;
        }

        private static string ReadUnityMetaGuid(string metaPath)
        {
            if (!File.Exists(metaPath))
                return string.Empty;

            string[] lines;
            try
            {
                lines = File.ReadAllLines(metaPath, Encoding.UTF8);
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is NotSupportedException)
            {
                return string.Empty;
            }

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("guid: ", StringComparison.Ordinal))
                    return lines[i].Substring("guid: ".Length).Trim();
            }

            return string.Empty;
        }

        private static int CountSceneRoots(string sceneText)
        {
            Match match = Regex.Match(
                sceneText,
                @"(?m)^\s+m_Roots:\s*\r?\n(?<roots>(?:^\s+-\s+\{fileID:\s*-?\d+[^\r\n]*\r?\n?)+)");
            if (!match.Success)
                return 0;

            return Regex.Matches(match.Groups["roots"].Value, @"\{fileID:\s*-?\d+\}").Count;
        }

        private static int CountString(string text, string value, StringComparison comparison)
        {
            int count = 0;
            int index = 0;
            while (index < text.Length)
            {
                int next = text.IndexOf(value, index, comparison);
                if (next < 0)
                    break;

                count++;
                index = next + value.Length;
            }

            return count;
        }

        private static int CountTerminalRowsInPlacementPlan(
            string planPath,
            List<Finding> findings,
            ref ApexSummary summary)
        {
            string[] lines;
            try
            {
                lines = File.ReadAllLines(planPath, Encoding.UTF8);
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is NotSupportedException)
            {
                findings.Add(new Finding(
                    TerminalOsScenePlacementPlanPath,
                    0,
                    "terminal_os_plan_read",
                    exception.GetType().Name));
                summary.DependencyFindings++;
                return 0;
            }

            int count = 0;
            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].IndexOf(",MessageTerminal,", StringComparison.Ordinal) >= 0)
                    count++;
            }

            return count;
        }

        private static string ExtractTerminalOsRuntimeBlock(string sceneText)
        {
            int scriptIndex = sceneText.IndexOf(TerminalOsRuntimeScriptGuid, StringComparison.OrdinalIgnoreCase);
            if (scriptIndex < 0)
                return string.Empty;

            int blockStart = sceneText.LastIndexOf("--- !u!114", scriptIndex, StringComparison.Ordinal);
            if (blockStart < 0)
                return string.Empty;

            int blockEnd = sceneText.IndexOf("\n--- !u!", scriptIndex, StringComparison.Ordinal);
            return blockEnd < 0 ? sceneText.Substring(blockStart) : sceneText.Substring(blockStart, blockEnd - blockStart);
        }

        private static int CountSerializedReferenceArray(
            string block,
            string fieldName,
            string relativePath,
            List<Finding> findings,
            ref ApexSummary summary)
        {
            if (string.IsNullOrEmpty(block))
            {
                findings.Add(new Finding(relativePath, 0, "terminal_os_runtime_block", "Serialized block missing"));
                summary.DependencyFindings++;
                return 0;
            }

            Match match = Regex.Match(
                block,
                @"(?m)^\s+" + Regex.Escape(fieldName) + @":\s*\r?\n(?<items>(?:^\s+-\s+\{fileID:\s*-?\d+[^\r\n]*\r?\n)+)");
            if (!match.Success)
            {
                findings.Add(new Finding(relativePath, 0, "terminal_os_array_missing", fieldName));
                summary.DependencyFindings++;
                return 0;
            }

            string items = match.Groups["items"].Value;
            MatchCollection refs = Regex.Matches(items, @"\{fileID:\s*(-?\d+)");
            for (int i = 0; i < refs.Count; i++)
            {
                if (refs[i].Groups[1].Value == "0")
                {
                    findings.Add(new Finding(relativePath, 0, "terminal_os_null_ref", fieldName));
                    summary.DependencyFindings++;
                    break;
                }
            }

            return refs.Count;
        }

        private static bool IsApexRoot(string methodName)
        {
            return string.Equals(methodName, "FastTick", StringComparison.Ordinal) ||
                   string.Equals(methodName, "Tick", StringComparison.Ordinal) ||
                   string.Equals(methodName, "Update", StringComparison.Ordinal) ||
                   string.Equals(methodName, "FixedTick", StringComparison.Ordinal) ||
                   string.Equals(methodName, "FixedUpdate", StringComparison.Ordinal) ||
                   string.Equals(methodName, "SlowTick", StringComparison.Ordinal) ||
                   string.Equals(methodName, "LateFrameTick", StringComparison.Ordinal) ||
                   string.Equals(methodName, "LateUpdate", StringComparison.Ordinal) ||
                   string.Equals(methodName, "VisualSyncTick", StringComparison.Ordinal) ||
                   string.Equals(methodName, "OnScan", StringComparison.Ordinal) ||
                   string.Equals(methodName, "Execute", StringComparison.Ordinal);
        }

        private static bool IsForbiddenDependencyInvocation(string invocationName, string invocationText)
        {
            for (int i = 0; i < DependencyInvocationNames.Length; i++)
            {
                if (string.Equals(invocationName, DependencyInvocationNames[i], StringComparison.Ordinal))
                    return true;
            }

            return invocationText.IndexOf("GlobalRegistry.Get<", StringComparison.Ordinal) >= 0 ||
                   invocationText.IndexOf("GameObject.Find", StringComparison.Ordinal) >= 0 ||
                   invocationText.IndexOf("Resources.FindObjectsOfTypeAll", StringComparison.Ordinal) >= 0;
        }

        private static bool IsGlobalRegistryAccess(MemberAccessExpressionSyntax access)
        {
            string expression = access.Expression.ToString();
            return string.Equals(expression, "GlobalRegistry", StringComparison.Ordinal) ||
                   expression.EndsWith(".GlobalRegistry", StringComparison.Ordinal);
        }

        private static bool IsPresentationInvocation(string invocationName, string invocationText)
        {
            for (int i = 0; i < PresentationInvocationNames.Length; i++)
            {
                if (string.Equals(invocationName, PresentationInvocationNames[i], StringComparison.Ordinal))
                    return true;
            }

            return invocationText.IndexOf(".text =", StringComparison.Ordinal) >= 0;
        }

        private static bool IsUnityEventInvoke(string invocationText)
        {
            return invocationText.IndexOf("?.Invoke", StringComparison.Ordinal) >= 0 ||
                   invocationText.IndexOf(".Invoke", StringComparison.Ordinal) >= 0;
        }

        private static bool IsHotManagedAllocationInvocation(string invocationName, string invocationText)
        {
            if (string.Equals(invocationName, "ToString", StringComparison.Ordinal) ||
                string.Equals(invocationName, "Format", StringComparison.Ordinal) ||
                string.Equals(invocationName, "Concat", StringComparison.Ordinal) ||
                string.Equals(invocationName, "Join", StringComparison.Ordinal) ||
                string.Equals(invocationName, "Resize", StringComparison.Ordinal) ||
                string.Equals(invocationName, "Where", StringComparison.Ordinal) ||
                string.Equals(invocationName, "Select", StringComparison.Ordinal) ||
                string.Equals(invocationName, "Any", StringComparison.Ordinal) ||
                string.Equals(invocationName, "All", StringComparison.Ordinal) ||
                string.Equals(invocationName, "Count", StringComparison.Ordinal) ||
                string.Equals(invocationName, "First", StringComparison.Ordinal) ||
                string.Equals(invocationName, "FirstOrDefault", StringComparison.Ordinal) ||
                string.Equals(invocationName, "Last", StringComparison.Ordinal) ||
                string.Equals(invocationName, "LastOrDefault", StringComparison.Ordinal) ||
                string.Equals(invocationName, "Single", StringComparison.Ordinal) ||
                string.Equals(invocationName, "SingleOrDefault", StringComparison.Ordinal) ||
                string.Equals(invocationName, "OrderBy", StringComparison.Ordinal) ||
                string.Equals(invocationName, "OrderByDescending", StringComparison.Ordinal) ||
                string.Equals(invocationName, "ThenBy", StringComparison.Ordinal) ||
                string.Equals(invocationName, "ThenByDescending", StringComparison.Ordinal) ||
                string.Equals(invocationName, "GroupBy", StringComparison.Ordinal) ||
                string.Equals(invocationName, "ToList", StringComparison.Ordinal) ||
                string.Equals(invocationName, "ToArray", StringComparison.Ordinal))
            {
                return true;
            }

            return invocationText.IndexOf("string.Format", StringComparison.Ordinal) >= 0 ||
                   invocationText.IndexOf("String.Format", StringComparison.Ordinal) >= 0 ||
                   invocationText.IndexOf("string.Concat", StringComparison.Ordinal) >= 0 ||
                   invocationText.IndexOf("String.Concat", StringComparison.Ordinal) >= 0 ||
                   invocationText.IndexOf("Array.Resize", StringComparison.Ordinal) >= 0 ||
                   invocationText.IndexOf("System.Array.Resize", StringComparison.Ordinal) >= 0 ||
                   invocationText.IndexOf("Enumerable.", StringComparison.Ordinal) >= 0;
        }

        private static bool IsHotStringConcatenation(BinaryExpressionSyntax binaryExpression)
        {
            return binaryExpression != null &&
                   binaryExpression.IsKind(SyntaxKind.AddExpression) &&
                   (ContainsStringLiteral(binaryExpression.Left) || ContainsStringLiteral(binaryExpression.Right));
        }

        private static bool ContainsStringLiteral(ExpressionSyntax expression)
        {
            if (expression == null)
                return false;

            if (expression is LiteralExpressionSyntax literal &&
                literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return true;
            }

            if (expression is InterpolatedStringExpressionSyntax)
                return true;

            if (expression is BinaryExpressionSyntax binary)
                return ContainsStringLiteral(binary.Left) || ContainsStringLiteral(binary.Right);

            return false;
        }

        private static bool IsHotManagedAllocationSyntax(SyntaxNode node)
        {
            if (node is InterpolatedStringExpressionSyntax)
                return true;

            if (node is ArrayCreationExpressionSyntax ||
                node is ImplicitArrayCreationExpressionSyntax)
            {
                return true;
            }

            if (!(node is ObjectCreationExpressionSyntax objectCreation))
                return false;

            string typeName = objectCreation.Type == null ? string.Empty : objectCreation.Type.ToString();
            return typeName.StartsWith("List<", StringComparison.Ordinal) ||
                   typeName.StartsWith("Dictionary<", StringComparison.Ordinal) ||
                   typeName.StartsWith("HashSet<", StringComparison.Ordinal) ||
                   typeName.StartsWith("Queue<", StringComparison.Ordinal) ||
                   typeName.StartsWith("Stack<", StringComparison.Ordinal) ||
                   typeName.StartsWith("System.Collections.Generic.List<", StringComparison.Ordinal) ||
                   typeName.StartsWith("System.Collections.Generic.Dictionary<", StringComparison.Ordinal) ||
                   typeName.StartsWith("System.Collections.Generic.HashSet<", StringComparison.Ordinal) ||
                   string.Equals(typeName, "StringBuilder", StringComparison.Ordinal) ||
                   string.Equals(typeName, "System.Text.StringBuilder", StringComparison.Ordinal) ||
                   string.Equals(typeName, "string", StringComparison.Ordinal) ||
                   string.Equals(typeName, "String", StringComparison.Ordinal) ||
                   string.Equals(typeName, "object", StringComparison.Ordinal) ||
                   string.Equals(typeName, "Object", StringComparison.Ordinal);
        }

        private static bool IsDirectJobHandleComplete(string invocationName, string invocationText)
        {
            return (string.Equals(invocationName, "Complete", StringComparison.Ordinal) &&
                    invocationText.IndexOf(".Complete(", StringComparison.Ordinal) >= 0) ||
                   string.Equals(invocationName, "WaitForCompletion", StringComparison.Ordinal) ||
                   invocationText.IndexOf(".WaitForCompletion(", StringComparison.Ordinal) >= 0;
        }

        private static bool IsTextAssignment(AssignmentExpressionSyntax assignment)
        {
            if (!(assignment.Left is MemberAccessExpressionSyntax access))
                return false;

            string name = ResolveSimpleName(access.Name);
            return string.Equals(name, "text", StringComparison.Ordinal) ||
                   string.Equals(name, "richText", StringComparison.Ordinal);
        }

        private static bool IsAllowedAppliedLoreArenaOwner(string relativePath)
        {
            return relativePath.EndsWith("H8AppliedLoreRuntime.cs", StringComparison.OrdinalIgnoreCase) ||
                   relativePath.EndsWith("H8StaticDataArena.cs", StringComparison.OrdinalIgnoreCase) ||
                   relativePath.EndsWith("H8DataMonolithCompiler.cs", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAlreadyParsedRuntimeSource(string relativePath, List<FileUnit> alreadyParsedFiles)
        {
            for (int i = 0; i < alreadyParsedFiles.Count; i++)
            {
                if (string.Equals(alreadyParsedFiles[i].RelativePath, relativePath, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool TryFindFile(List<FileUnit> files, string fileName, out FileUnit file)
        {
            for (int i = 0; i < files.Count; i++)
            {
                if (files[i].RelativePath.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    file = files[i];
                    return true;
                }
            }

            file = default;
            return false;
        }

        private static bool TryFindMethod(CompilationUnitSyntax root, string methodName, out MethodDeclarationSyntax method)
        {
            if (root != null && !string.IsNullOrEmpty(methodName))
            {
                using (IEnumerator<MethodDeclarationSyntax> methods =
                    root.DescendantNodes().OfType<MethodDeclarationSyntax>().GetEnumerator())
                {
                    while (methods.MoveNext())
                    {
                        MethodDeclarationSyntax candidate = methods.Current;
                        if (string.Equals(candidate.Identifier.ValueText, methodName, StringComparison.Ordinal))
                        {
                            method = candidate;
                            return true;
                        }
                    }
                }
            }

            method = null;
            return false;
        }

        private static int CountInvocationInMethod(CompilationUnitSyntax root, string methodName, string invocationName)
        {
            if (root == null)
                return 0;

            int count = 0;
            using (IEnumerator<MethodDeclarationSyntax> methods =
                root.DescendantNodes().OfType<MethodDeclarationSyntax>().GetEnumerator())
            {
                while (methods.MoveNext())
                {
                    MethodDeclarationSyntax method = methods.Current;
                    if (!string.Equals(method.Identifier.ValueText, methodName, StringComparison.Ordinal))
                        continue;

                    SyntaxNode body = (SyntaxNode)method.Body ?? method.ExpressionBody;
                    if (body == null)
                        continue;

                    using (IEnumerator<InvocationExpressionSyntax> invocations =
                        body.DescendantNodes().OfType<InvocationExpressionSyntax>().GetEnumerator())
                    {
                        while (invocations.MoveNext())
                        {
                            if (MatchesInvocation(invocations.Current, invocationName))
                                count++;
                        }
                    }
                }
            }

            return count;
        }

        private static int CountInvocationTextInMethod(CompilationUnitSyntax root, string methodName, string requiredText)
        {
            if (root == null || string.IsNullOrEmpty(requiredText))
                return 0;

            int count = 0;
            using (IEnumerator<MethodDeclarationSyntax> methods =
                root.DescendantNodes().OfType<MethodDeclarationSyntax>().GetEnumerator())
            {
                while (methods.MoveNext())
                {
                    MethodDeclarationSyntax method = methods.Current;
                    if (!string.Equals(method.Identifier.ValueText, methodName, StringComparison.Ordinal))
                        continue;

                    SyntaxNode body = (SyntaxNode)method.Body ?? method.ExpressionBody;
                    if (body == null)
                        continue;

                    using (IEnumerator<InvocationExpressionSyntax> invocations =
                        body.DescendantNodes().OfType<InvocationExpressionSyntax>().GetEnumerator())
                    {
                        while (invocations.MoveNext())
                        {
                            if (invocations.Current.ToString().IndexOf(requiredText, StringComparison.Ordinal) >= 0)
                                count++;
                        }
                    }
                }
            }

            return count;
        }

        private static int CountTextInMethod(CompilationUnitSyntax root, string methodName, string requiredText)
        {
            if (root == null || string.IsNullOrEmpty(requiredText))
                return 0;

            int count = 0;
            using (IEnumerator<MethodDeclarationSyntax> methods =
                root.DescendantNodes().OfType<MethodDeclarationSyntax>().GetEnumerator())
            {
                while (methods.MoveNext())
                {
                    MethodDeclarationSyntax method = methods.Current;
                    if (!string.Equals(method.Identifier.ValueText, methodName, StringComparison.Ordinal))
                        continue;

                    count += CountOccurrences(method.ToFullString(), requiredText);
                }
            }

            return count;
        }

        private static bool MethodTextAppearsInOrder(
            CompilationUnitSyntax root,
            string methodName,
            string firstText,
            string secondText)
        {
            if (root == null ||
                string.IsNullOrEmpty(firstText) ||
                string.IsNullOrEmpty(secondText) ||
                !TryFindMethod(root, methodName, out MethodDeclarationSyntax method))
            {
                return false;
            }

            string methodText = method.ToFullString();
            int firstIndex = methodText.IndexOf(firstText, StringComparison.Ordinal);
            if (firstIndex < 0)
                return false;

            return methodText.IndexOf(
                secondText,
                firstIndex + firstText.Length,
                StringComparison.Ordinal) >= 0;
        }

        private static int CountTextInStruct(CompilationUnitSyntax root, string structName, string requiredText)
        {
            if (root == null || string.IsNullOrEmpty(structName) || string.IsNullOrEmpty(requiredText))
                return 0;

            using (IEnumerator<StructDeclarationSyntax> structs =
                root.DescendantNodes().OfType<StructDeclarationSyntax>().GetEnumerator())
            {
                while (structs.MoveNext())
                {
                    StructDeclarationSyntax declaration = structs.Current;
                    if (!string.Equals(declaration.Identifier.ValueText, structName, StringComparison.Ordinal))
                        continue;

                    return CountOccurrences(declaration.ToFullString(), requiredText);
                }
            }

            return 0;
        }

        private static int CountDataVaultWriteReleaseFinallyInMethod(MethodDeclarationSyntax method)
        {
            if (method == null)
                return 0;

            SyntaxNode body = (SyntaxNode)method.Body ?? method.ExpressionBody;
            if (body == null)
                return 0;

            int count = 0;
            using (IEnumerator<InvocationExpressionSyntax> invocations =
                body.DescendantNodes().OfType<InvocationExpressionSyntax>().GetEnumerator())
            {
                while (invocations.MoveNext())
                {
                    InvocationExpressionSyntax invocation = invocations.Current;
                    if (IsDataVaultWriteLock(ResolveInvocationName(invocation), invocation.ToString()) &&
                        HasDataVaultWriteReleaseFinally(invocation))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static int CountHeavyTelemetryInsideDataVaultWriteReleaseTry(MethodDeclarationSyntax method)
        {
            if (method == null)
                return 0;

            SyntaxNode body = (SyntaxNode)method.Body ?? method.ExpressionBody;
            if (body == null)
                return 0;

            int count = 0;
            using (IEnumerator<TryStatementSyntax> tries =
                body.DescendantNodes().OfType<TryStatementSyntax>().GetEnumerator())
            {
                while (tries.MoveNext())
                {
                    TryStatementSyntax tryStatement = tries.Current;
                    if (!FinallyContainsDataVaultWriteRelease(tryStatement.Finally))
                        continue;

                    string blockText = tryStatement.Block == null ? string.Empty : tryStatement.Block.ToFullString();
                    count += CountOccurrences(blockText, "ResolveTelemetrySpeedMetersPerSecond(");
                    count += CountOccurrences(blockText, "CurrentFrame");
                    count += CountOccurrences(blockText, "math.clamp");
                    count += CountOccurrences(blockText, "math.lengthsq");
                    count += CountOccurrences(blockText, "math.rsqrt");
                    count += CountOccurrences(blockText, "math.sqrt");
                }
            }

            return count;
        }

        private static int CountInvocationTextInMethodOutsideLock(
            CompilationUnitSyntax root,
            string methodName,
            string requiredText)
        {
            if (root == null || string.IsNullOrEmpty(requiredText))
                return 0;

            int count = 0;
            using (IEnumerator<MethodDeclarationSyntax> methods =
                root.DescendantNodes().OfType<MethodDeclarationSyntax>().GetEnumerator())
            {
                while (methods.MoveNext())
                {
                    MethodDeclarationSyntax method = methods.Current;
                    if (!string.Equals(method.Identifier.ValueText, methodName, StringComparison.Ordinal))
                        continue;

                    SyntaxNode body = (SyntaxNode)method.Body ?? method.ExpressionBody;
                    if (body == null)
                        continue;

                    using (IEnumerator<InvocationExpressionSyntax> invocations =
                        body.DescendantNodes().OfType<InvocationExpressionSyntax>().GetEnumerator())
                    {
                        while (invocations.MoveNext())
                        {
                            InvocationExpressionSyntax invocation = invocations.Current;
                            if (invocation.ToString().IndexOf(requiredText, StringComparison.Ordinal) < 0 ||
                                invocation.FirstAncestorOrSelf<LockStatementSyntax>() != null)
                            {
                                continue;
                            }

                            count++;
                        }
                    }
                }
            }

            return count;
        }

        private static int CountLockStatementsInMethod(CompilationUnitSyntax root, string methodName)
        {
            if (root == null)
                return 0;

            int count = 0;
            using (IEnumerator<MethodDeclarationSyntax> methods =
                root.DescendantNodes().OfType<MethodDeclarationSyntax>().GetEnumerator())
            {
                while (methods.MoveNext())
                {
                    MethodDeclarationSyntax method = methods.Current;
                    if (!string.Equals(method.Identifier.ValueText, methodName, StringComparison.Ordinal))
                        continue;

                    SyntaxNode body = (SyntaxNode)method.Body ?? method.ExpressionBody;
                    if (body == null)
                        continue;

                    using (IEnumerator<LockStatementSyntax> locks =
                        body.DescendantNodes().OfType<LockStatementSyntax>().GetEnumerator())
                    {
                        while (locks.MoveNext())
                            count++;
                    }

                    using (IEnumerator<InvocationExpressionSyntax> invocations =
                        body.DescendantNodes().OfType<InvocationExpressionSyntax>().GetEnumerator())
                    {
                        while (invocations.MoveNext())
                        {
                            if (IsMonitorEnterInvocation(invocations.Current))
                                count++;
                        }
                    }
                }
            }

            return count;
        }

        private static bool IsMonitorEnterInvocation(InvocationExpressionSyntax invocation)
        {
            if (invocation == null)
                return false;

            string text = invocation.ToString();
            return text.IndexOf("Monitor.Enter", StringComparison.Ordinal) >= 0 ||
                   text.IndexOf("System.Threading.Monitor.Enter", StringComparison.Ordinal) >= 0;
        }

        private static bool MatchesInvocation(InvocationExpressionSyntax invocation, string invocationName)
        {
            if (invocation == null || string.IsNullOrEmpty(invocationName))
                return false;

            string resolvedName = ResolveInvocationName(invocation);
            if (string.Equals(resolvedName, invocationName, StringComparison.Ordinal))
                return true;

            if (invocationName.IndexOf('.') < 0 &&
                invocationName.IndexOf('<') < 0)
            {
                return false;
            }

            return invocation.ToString().IndexOf(invocationName, StringComparison.Ordinal) >= 0;
        }

        private static int CountTextInFiles(List<FileUnit> files, string requiredText)
        {
            if (string.IsNullOrEmpty(requiredText))
                return 0;

            int count = 0;
            for (int i = 0; i < files.Count; i++)
                count += CountOccurrences(files[i].Root.ToFullString(), requiredText);
            return count;
        }

        private static int CountTextInFile(FileUnit file, string requiredText)
        {
            if (string.IsNullOrEmpty(requiredText))
                return 0;

            return CountOccurrences(file.Root.ToFullString(), requiredText);
        }

        private static int CountTextInFilesExcept(
            List<FileUnit> files,
            string requiredText,
            string excludedFileName)
        {
            if (string.IsNullOrEmpty(requiredText))
                return 0;

            int count = 0;
            for (int i = 0; i < files.Count; i++)
            {
                string relativePath = files[i].RelativePath ?? string.Empty;
                if (!string.IsNullOrEmpty(excludedFileName) &&
                    relativePath.EndsWith(excludedFileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                count += CountOccurrences(files[i].Root.ToFullString(), requiredText);
            }

            return count;
        }

        private static int CountOccurrences(string source, string requiredText)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(requiredText))
                return 0;

            int count = 0;
            int index = 0;
            while (index < source.Length)
            {
                int found = source.IndexOf(requiredText, index, StringComparison.Ordinal);
                if (found < 0)
                    break;

                count++;
                index = found + requiredText.Length;
            }

            return count;
        }

        private static bool IsDirectAppliedLoreArenaInvocation(InvocationExpressionSyntax invocation)
        {
            if (!(invocation.Expression is MemberAccessExpressionSyntax access) ||
                !string.Equals(access.Expression.ToString(), "H8StaticDataArena", StringComparison.Ordinal))
            {
                return false;
            }

            string name = ResolveSimpleName(access.Name);
            if (name.IndexOf("AppliedLore", StringComparison.Ordinal) >= 0)
                return true;

            if (!string.Equals(name, "GetSectionSpan", StringComparison.Ordinal) ||
                !(access.Name is GenericNameSyntax genericName))
            {
                return false;
            }

            SeparatedSyntaxList<TypeSyntax> arguments = genericName.TypeArgumentList.Arguments;
            for (int i = 0; i < arguments.Count; i++)
            {
                string argument = arguments[i].ToString();
                if (string.Equals(argument, "H8AppliedLorePacketRecord", StringComparison.Ordinal) ||
                    string.Equals(argument, "H8AppliedLoreRouteRecord", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDirectAppliedLoreHashOnlyUnlock(InvocationExpressionSyntax invocation)
        {
            if (!(invocation.Expression is MemberAccessExpressionSyntax access))
                return false;

            string owner = access.Expression.ToString();
            string name = ResolveSimpleName(access.Name);
            return string.Equals(name, "TryRaisePacketUnlocked", StringComparison.Ordinal) &&
                   (string.Equals(owner, "H8AppliedLoreRuntime", StringComparison.Ordinal) ||
                    owner.EndsWith(".H8AppliedLoreRuntime", StringComparison.Ordinal));
        }

        private static bool IsDirectLoreFragmentSignalPublish(InvocationExpressionSyntax invocation)
        {
            if (!(invocation.Expression is MemberAccessExpressionSyntax access))
                return false;

            string name = ResolveSimpleName(access.Name);
            if (!string.Equals(name, "TryPushTracked", StringComparison.Ordinal) &&
                !string.Equals(name, "TryPush", StringComparison.Ordinal))
            {
                return false;
            }

            string owner = access.Expression.ToString();
            return owner.IndexOf("SignalBus<LoreFragmentScannedSignal>", StringComparison.Ordinal) >= 0 ||
                   owner.IndexOf("SignalBus<global::Hecton8.Core.Contracts.Signals.LoreFragmentScannedSignal>", StringComparison.Ordinal) >= 0;
        }

        private static bool IsAllowedScannerLoreFragmentSignalPublish(InvocationExpressionSyntax invocation)
        {
            string relativePath = RelativePath(invocation);
            if (!relativePath.EndsWith("ScannerDataMiningRouter.cs", StringComparison.OrdinalIgnoreCase))
                return false;

            MethodDeclarationSyntax method = invocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            return method != null &&
                   string.Equals(method.Identifier.ValueText, "RouteCompletionIfNeeded", StringComparison.Ordinal);
        }

        private static bool IsDataVaultWriteLock(string invocationName, string invocationText)
        {
            if (string.Equals(invocationName, "ReleaseWriteLock", StringComparison.Ordinal) ||
                invocationText.IndexOf(".ReleaseWriteLock", StringComparison.Ordinal) >= 0)
            {
                return false;
            }

            for (int i = 0; i < DataVaultWriteLockNames.Length; i++)
            {
                if (string.Equals(invocationName, DataVaultWriteLockNames[i], StringComparison.Ordinal))
                    return true;
            }

            return invocationText.IndexOf("DataVault", StringComparison.Ordinal) >= 0 &&
                   invocationText.IndexOf("WriteLock", StringComparison.Ordinal) >= 0;
        }

        private static bool HasDataVaultWriteReleaseFinally(InvocationExpressionSyntax invocation)
        {
            if (invocation == null)
                return false;

            TryStatementSyntax lockTry = invocation.FirstAncestorOrSelf<TryStatementSyntax>();
            if (HasReleaseFinallyForDataVaultWriteLock(lockTry))
                return true;

            IfStatementSyntax ifStatement = invocation.FirstAncestorOrSelf<IfStatementSyntax>();
            if (StatementContainsDataVaultWriteReleaseFinally(ifStatement == null ? null : ifStatement.Statement))
                return true;

            return HasImmediateTryFinallyWithInvocationAfterCall(invocation, "ReleaseWriteLock");
        }

        private static bool HasReleaseFinallyForDataVaultWriteLock(TryStatementSyntax lockTry)
        {
            return lockTry != null &&
                   lockTry.Finally != null &&
                   FinallyContainsDataVaultWriteRelease(lockTry.Finally);
        }

        private static bool IsDataVaultWriteAcquireTransferHelper(MethodDeclarationSyntax method)
        {
            if (method == null)
                return false;

            string name = method.Identifier.ValueText;
            if (!name.StartsWith("TryAcquire", StringComparison.Ordinal) ||
                !name.EndsWith("Write", StringComparison.Ordinal))
            {
                return false;
            }

            SeparatedSyntaxList<ParameterSyntax> parameters = method.ParameterList.Parameters;
            for (int i = 0; i < parameters.Count; i++)
            {
                ParameterSyntax parameter = parameters[i];
                if (parameter.Modifiers.Any(SyntaxKind.OutKeyword) &&
                    parameter.Type != null &&
                    parameter.Type.ToString().IndexOf("IDataVault", StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasTransferHelperFailureReleaseFinally(MethodDeclarationSyntax method)
        {
            if (method == null)
                return false;

            SyntaxNode body = (SyntaxNode)method.Body ?? method.ExpressionBody;
            if (body == null)
                return false;

            using (IEnumerator<TryStatementSyntax> tries =
                body.DescendantNodes().OfType<TryStatementSyntax>().GetEnumerator())
            {
                while (tries.MoveNext())
                {
                    TryStatementSyntax tryStatement = tries.Current;
                    if (tryStatement.Finally != null &&
                        FinallyContainsDataVaultWriteRelease(tryStatement.Finally))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsDataVaultWriteAcquireHelperCall(
            InvocationExpressionSyntax invocation,
            out string releaseInvocationName)
        {
            releaseInvocationName = string.Empty;
            if (invocation == null)
                return false;

            string acquireName = ResolveInvocationName(invocation);
            if (string.Equals(acquireName, "TryAcquireFrameSnapshotForOwnerWrite", StringComparison.Ordinal))
                return false;

            if (!acquireName.StartsWith("TryAcquire", StringComparison.Ordinal) ||
                !acquireName.EndsWith("Write", StringComparison.Ordinal))
            {
                return false;
            }

            if (IsDataVaultWriteLock(acquireName, invocation.ToString()))
                return false;

            int middleLength = acquireName.Length - "TryAcquire".Length - "Write".Length;
            if (middleLength <= 0)
                return false;

            releaseInvocationName = "Release" + acquireName.Substring("TryAcquire".Length, middleLength) + "Write";
            return true;
        }

        private static bool HasImmediateTryFinallyWithInvocationAfterCall(
            InvocationExpressionSyntax invocation,
            string releaseInvocationName)
        {
            if (string.IsNullOrEmpty(releaseInvocationName))
                return false;

            StatementSyntax statement = invocation.FirstAncestorOrSelf<StatementSyntax>();
            if (statement == null)
                return false;

            IfStatementSyntax ifStatement = invocation.FirstAncestorOrSelf<IfStatementSyntax>();
            if (StatementContainsInvocationInFinally(ifStatement == null ? null : ifStatement.Statement, releaseInvocationName))
                return true;

            BlockSyntax block = statement.Parent as BlockSyntax;
            if (block == null)
                return false;

            SyntaxList<StatementSyntax> statements = block.Statements;
            for (int i = 0; i < statements.Count - 1; i++)
            {
                if (!ReferenceEquals(statements[i], statement))
                    continue;

                return statements[i + 1] is TryStatementSyntax tryStatement &&
                       tryStatement.Finally != null &&
                       FinallyContainsInvocation(tryStatement.Finally, releaseInvocationName);
            }

            return false;
        }

        private static bool StatementContainsDataVaultWriteReleaseFinally(StatementSyntax statement)
        {
            if (statement == null)
                return false;

            using (IEnumerator<TryStatementSyntax> tries =
                statement.DescendantNodesAndSelf().OfType<TryStatementSyntax>().GetEnumerator())
            {
                while (tries.MoveNext())
                {
                    if (HasReleaseFinallyForDataVaultWriteLock(tries.Current))
                        return true;
                }
            }

            return false;
        }

        private static bool StatementContainsInvocationInFinally(StatementSyntax statement, string invocationName)
        {
            if (statement == null || string.IsNullOrEmpty(invocationName))
                return false;

            using (IEnumerator<TryStatementSyntax> tries =
                statement.DescendantNodesAndSelf().OfType<TryStatementSyntax>().GetEnumerator())
            {
                while (tries.MoveNext())
                {
                    TryStatementSyntax tryStatement = tries.Current;
                    if (tryStatement.Finally != null &&
                        FinallyContainsInvocation(tryStatement.Finally, invocationName))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool FinallyContainsDataVaultWriteRelease(FinallyClauseSyntax finallyClause)
        {
            if (finallyClause == null)
                return false;

            using (IEnumerator<InvocationExpressionSyntax> invocations =
                finallyClause.DescendantNodes().OfType<InvocationExpressionSyntax>().GetEnumerator())
            {
                while (invocations.MoveNext())
                {
                    string name = ResolveInvocationName(invocations.Current);
                    string text = invocations.Current.ToString();
                    if (string.Equals(name, "ReleaseWriteLock", StringComparison.Ordinal) ||
                        text.IndexOf(".ReleaseWriteLock", StringComparison.Ordinal) >= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool FinallyContainsInvocation(FinallyClauseSyntax finallyClause, string invocationName)
        {
            if (finallyClause == null || string.IsNullOrEmpty(invocationName))
                return false;

            using (IEnumerator<InvocationExpressionSyntax> invocations =
                finallyClause.DescendantNodes().OfType<InvocationExpressionSyntax>().GetEnumerator())
            {
                while (invocations.MoveNext())
                {
                    string name = ResolveInvocationName(invocations.Current);
                    if (string.Equals(name, invocationName, StringComparison.Ordinal))
                        return true;
                }
            }

            return false;
        }

        private static int CountDataVaultWriteLocks(SyntaxNode node)
        {
            if (node == null)
                return 0;

            int count = 0;
            using (IEnumerator<InvocationExpressionSyntax> invocations =
                node.DescendantNodes().OfType<InvocationExpressionSyntax>().GetEnumerator())
            {
                while (invocations.MoveNext())
                {
                    InvocationExpressionSyntax invocation = invocations.Current;
                    if (IsDataVaultWriteLock(ResolveInvocationName(invocation), invocation.ToString()))
                        count++;
                }
            }

            return count;
        }

        private static string ResolveInvocationName(InvocationExpressionSyntax invocation)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                return ResolveSimpleName(memberAccess.Name);
            if (invocation.Expression is MemberBindingExpressionSyntax memberBinding)
                return ResolveSimpleName(memberBinding.Name);
            if (invocation.Expression is IdentifierNameSyntax identifier)
                return identifier.Identifier.ValueText;
            if (invocation.Expression is GenericNameSyntax genericName)
                return genericName.Identifier.ValueText;
            if (invocation.Expression is ConditionalAccessExpressionSyntax conditionalAccess &&
                conditionalAccess.WhenNotNull is InvocationExpressionSyntax nestedInvocation)
            {
                return ResolveInvocationName(nestedInvocation);
            }

            return string.Empty;
        }

        private static string ResolveSimpleName(SimpleNameSyntax name)
        {
            if (name is GenericNameSyntax genericName)
                return genericName.Identifier.ValueText;
            return name.Identifier.ValueText;
        }

        private static int LogFindings(List<Finding> findings)
        {
            int count = Math.Min(findings.Count, MaxFindingsLogged);
            for (int i = 0; i < count; i++)
                Debug.LogError("[H8NarrativeApexVerifier] " + findings[i].ToConsoleLine());
            if (findings.Count > count)
                Debug.LogError("[H8NarrativeApexVerifier] additional findings not logged=" + (findings.Count - count));
            return count;
        }

        private static string BuildConsoleReport(in ApexSummary summary)
        {
            StringBuilder builder = new StringBuilder(512);
            builder.Append("[H8NarrativeApexVerifier] files_expected=").Append(summary.FilesExpected);
            builder.Append(" files_parsed=").Append(summary.FilesParsed);
            builder.Append(" missing=").Append(summary.MissingFiles);
            builder.Append(" parse_failures=").Append(summary.ParseFailures);
            builder.Append(" methods_indexed=").Append(summary.MethodsIndexed);
            builder.Append(" hot_roots=").Append(summary.HotRootsScanned);
            builder.Append(" methods_from_roots=").Append(summary.MethodsVisitedFromHotRoots);
            builder.Append(" applied_lore_boundary_files=").Append(summary.AppliedLoreBoundaryFilesScanned);
            builder.Append(" applied_lore_boundary_candidates=").Append(summary.AppliedLoreBoundaryCandidateFilesParsed);
            builder.Append(" applied_lore_unlock_files=").Append(summary.AppliedLoreUnlockFilesScanned);
            builder.Append(" applied_lore_unlock_candidates=").Append(summary.AppliedLoreUnlockCandidateFilesParsed);
            builder.Append(" scanner_lore_fragment_signal_layout=").Append(summary.ScannerLoreFragmentSignalLayout);
            builder.Append(" scanner_lore_fragment_completion_publishes=").Append(summary.ScannerLoreFragmentCompletionPublishes);
            builder.Append(" scanner_lore_fragment_completion_fields=").Append(summary.ScannerLoreFragmentCompletionFields);
            builder.Append(" scanner_lore_fragment_paired_scan_complete=").Append(summary.ScannerLoreFragmentPairedScanComplete);
            builder.Append(" scanner_lore_fragment_pda_snapshot_reads=").Append(summary.ScannerLoreFragmentPdaSnapshotReads);
            builder.Append(" scanner_lore_fragment_pda_aup_reads=").Append(summary.ScannerLoreFragmentPdaAupReads);
            builder.Append(" scanner_lore_fragment_pda_scan_complete_aup_checks=").Append(summary.ScannerLoreFragmentPdaScanCompleteAupFiniteChecks);
            builder.Append(" scanner_lore_fragment_pda_unlock_calls=").Append(summary.ScannerLoreFragmentPdaUnlockCalls);
            builder.Append(" scanner_lore_fragment_pda_paired_dedupes=").Append(summary.ScannerLoreFragmentPdaPairedDedupes);
            builder.Append(" scanner_lore_fragment_applied_lore_aup_publishes=").Append(summary.ScannerLoreFragmentAppliedLoreAupPublishes);
            builder.Append(" scanner_lore_fragment_applied_lore_paired_flags=").Append(summary.ScannerLoreFragmentAppliedLorePairedFlags);
            builder.Append(" scanner_lore_fragment_hash_only_flag_strips=").Append(summary.ScannerLoreFragmentHashOnlyFlagStrips);
            builder.Append(" scanner_lore_fragment_scan_events_cold_prewarm=").Append(summary.ScannerLoreFragmentScanEventsColdPrewarm);
            builder.Append(" scanner_lore_fragment_legacy_direct_dequeues=").Append(summary.ScannerLoreFragmentLegacyDirectDequeues);
            builder.Append(" scanner_lore_fragment_allowed_direct_publishes=").Append(summary.ScannerLoreFragmentAllowedDirectPublishes);
            builder.Append(" terminal_os_expected=").Append(summary.TerminalOsExpectedTerminals);
            builder.Append(" terminal_os_runtime_rows=").Append(summary.TerminalOsRuntimeRows);
            builder.Append(" terminal_os_renderer_slots=").Append(summary.TerminalOsRendererSlots);
            builder.Append(" terminal_os_transform_slots=").Append(summary.TerminalOsTransformSlots);
            builder.Append(" terminal_os_verified_slots=").Append(summary.TerminalOsVerifiedSlots);
            builder.Append(" terminal_os_preview_hash_pairs=").Append(summary.TerminalOsPreviewHashPairs);
            builder.Append(" terminal_os_preview_hash_mismatches=").Append(summary.TerminalOsPreviewHashMismatches);
            builder.Append(" terminal_os_preview_hash_duplicate_indices=").Append(summary.TerminalOsPreviewHashDuplicateIndices);
            builder.Append(" terminal_os_scene_binding_warnings=").Append(summary.TerminalOsSceneBindingWarnings);
            builder.Append(" terminal_preview_signal_defs=").Append(summary.TerminalPreviewSignalDefinitions);
            builder.Append(" terminal_preview_lifecycle_size_proofs=").Append(summary.TerminalPreviewSignalLifecycleSizeProofs);
            builder.Append(" applied_lore_runtime_layout_proofs=").Append(summary.AppliedLoreRuntimeLayoutProofs);
            builder.Append(" applied_lore_boot_layout_guards=").Append(summary.AppliedLoreBootLayoutGuards);
            builder.Append(" terminal_preview_bus_contracts=").Append(summary.TerminalPreviewSignalBusContracts);
            builder.Append(" terminal_preview_publishers=").Append(summary.TerminalPreviewPublisherCalls);
            builder.Append(" terminal_preview_snapshot_reads=").Append(summary.TerminalPreviewSnapshotReads);
            builder.Append(" terminal_preview_lateframe_calls=").Append(summary.TerminalPreviewLateFrameCalls);
            builder.Append(" terminal_preview_public_writers=").Append(summary.TerminalPreviewPublicWriterDefinitions);
            builder.Append(" terminal_preview_external_writers=").Append(summary.TerminalPreviewExternalWriterCalls);
            builder.Append(" terminal_os_graphics_rebuild_lateframe_calls=").Append(summary.TerminalOsGraphicsRebuildLateFrameCalls);
            builder.Append(" terminal_os_graphics_rebuild_slowtick_calls=").Append(summary.TerminalOsGraphicsRebuildSlowTickCalls);
            builder.Append(" terminal_os_graphics_rebuild_job_guards=").Append(summary.TerminalOsGraphicsRebuildJobGuards);
            builder.Append(" terminal_os_quality_rebuild_guards=").Append(summary.TerminalOsQualityRuntimeRebuildGuards);
            builder.Append(" terminal_os_quality_playing_texture_blocks=").Append(summary.TerminalOsQualityPlayingTextureBlocks);
            builder.Append(" scannable_fragment_hash_unlocks=").Append(summary.ScannableFragmentHashUnlocks);
            builder.Append(" scannable_fragment_lateframe_flushes=").Append(summary.ScannableFragmentLateFrameEventFlushes);
            builder.Append(" scannable_fragment_lifecycle_clears=").Append(summary.ScannableFragmentLifecycleClears);
            builder.Append(" scannable_fragment_pending_string_clears=").Append(summary.ScannableFragmentPendingStringClears);
            builder.Append(" scannable_fragment_lock_state_order=").Append(summary.ScannableFragmentLockStateOrder);
            builder.Append(" scannable_fragment_event_flush_before_disable=").Append(summary.ScannableFragmentEventFlushBeforeDisable);
            builder.Append(" narrative_discovery_lore_hash_caches=").Append(summary.NarrativeDiscoveryLoreHashCaches);
            builder.Append(" narrative_discovery_cached_unlock_calls=").Append(summary.NarrativeDiscoveryCachedUnlockCalls);
            builder.Append(" narrative_discovery_runtime_string_hashes=").Append(summary.NarrativeDiscoveryInteractionStringHashes);
            builder.Append(" hecton_director_poi_hash_caches=").Append(summary.HectonDirectorPoiHashCaches);
            builder.Append(" hecton_director_poi_cached_dispatches=").Append(summary.HectonDirectorPoiCachedDispatches);
            builder.Append(" hecton_director_poi_runtime_string_hashes=").Append(summary.HectonDirectorPoiRuntimeStringHashes);
            builder.Append(" applied_lore_world_impact_tick_drains=").Append(summary.AppliedLoreWorldImpactTickDrains);
            builder.Append(" applied_lore_world_impact_lateframe_drains=").Append(summary.AppliedLoreWorldImpactLateFrameDrains);
            builder.Append(" applied_lore_world_impact_queued_audio_transfers=").Append(summary.AppliedLoreWorldImpactQueuedAudioTransfers);
            builder.Append(" applied_lore_world_impact_lifecycle_clears=").Append(summary.AppliedLoreWorldImpactLifecycleClears);
            builder.Append(" applied_lore_world_impact_signal_publishes=").Append(summary.AppliedLoreWorldImpactSignalPublishes);
            builder.Append(" applied_lore_world_impact_dedup_guards=").Append(summary.AppliedLoreWorldImpactDedupGuards);
            builder.Append(" applied_lore_world_impact_layout_size_constants=").Append(summary.AppliedLoreWorldImpactLayoutSizeConstants);
            builder.Append(" applied_lore_world_impact_layout_padding_fields=").Append(summary.AppliedLoreWorldImpactLayoutPaddingFields);
            builder.Append(" applied_lore_world_impact_layout_sizeof_proofs=").Append(summary.AppliedLoreWorldImpactLayoutSizeofProofs);
            builder.Append(" applied_lore_world_impact_central_audit_proofs=").Append(summary.AppliedLoreWorldImpactCentralAuditProofs);
            builder.Append(" applied_lore_utf8_pass_by_ref_proofs=").Append(summary.AppliedLoreUtf8PassByRefProofs);
            builder.Append(" applied_lore_utf8_facade_duplicate_selectors=").Append(summary.AppliedLoreUtf8FacadeDuplicateSelectors);
            builder.Append(" meta_campaign_visual_queue_calls=").Append(summary.MetaCampaignVisualQueueCalls);
            builder.Append(" meta_campaign_visual_lateframe_flushes=").Append(summary.MetaCampaignVisualFlushLateFrameCalls);
            builder.Append(" meta_campaign_visual_publish_calls=").Append(summary.MetaCampaignVisualPublishCalls);
            builder.Append(" meta_campaign_visual_shader_writes=").Append(summary.MetaCampaignVisualShaderWrites);
            builder.Append(" meta_campaign_audio_queue_calls=").Append(summary.MetaCampaignAudioQueueCalls);
            builder.Append(" meta_campaign_audio_lateframe_flushes=").Append(summary.MetaCampaignAudioFlushLateFrameCalls);
            builder.Append(" meta_campaign_audio_publish_calls=").Append(summary.MetaCampaignAudioPublishCalls);
            builder.Append(" meta_campaign_cartography_queue_calls=").Append(summary.MetaCampaignCartographyQueueCalls);
            builder.Append(" meta_campaign_cartography_lateframe_flushes=").Append(summary.MetaCampaignCartographyFlushLateFrameCalls);
            builder.Append(" meta_campaign_cartography_publish_calls=").Append(summary.MetaCampaignCartographyPublishCalls);
            builder.Append(" message_terminal_finite_time_guards=").Append(summary.MessageTerminalFiniteTimeGuards);
            builder.Append(" message_terminal_presentation_scalar_guards=").Append(summary.MessageTerminalPresentationScalarGuards);
            builder.Append(" message_terminal_pending_event_clears=").Append(summary.MessageTerminalPendingEventClears);
            builder.Append(" message_terminal_hash_fields=").Append(summary.MessageTerminalMessageHashFields);
            builder.Append(" message_terminal_hash_cold_caches=").Append(summary.MessageTerminalMessageHashColdCaches);
            builder.Append(" message_terminal_hash_event_queues=").Append(summary.MessageTerminalHashEventQueues);
            builder.Append(" message_terminal_hash_event_flushes=").Append(summary.MessageTerminalHashEventFlushes);
            builder.Append(" message_terminal_hash_event_clears=").Append(summary.MessageTerminalHashEventClears);
            builder.Append(" message_terminal_hash_pending_reads=").Append(summary.MessageTerminalHashPendingReads);
            builder.Append(" message_terminal_legacy_pending_contains=").Append(summary.MessageTerminalLegacyPendingContains);
            builder.Append(" ui_rescale_producer_public_requests=").Append(summary.UiRescaleProducerPublicRequests);
            builder.Append(" ui_rescale_producer_reasons=").Append(summary.UiRescaleProducerReasons);
            builder.Append(" ui_rescale_producer_finite_guards=").Append(summary.UiRescaleProducerFiniteGuards);
            builder.Append(" ui_rescale_producer_signal_pushes=").Append(summary.UiRescaleProducerSignalPushes);
            builder.Append(" ui_rescale_producer_signal_initializers=").Append(summary.UiRescaleProducerSignalInitializers);
            builder.Append(" ui_rescale_producer_layout_applies=").Append(summary.UiRescaleProducerLayoutApplies);
            builder.Append(" accessibility_text_scale_fields=").Append(summary.AccessibilityTextScaleFields);
            builder.Append(" accessibility_text_scale_setters=").Append(summary.AccessibilityTextScalePublicSetters);
            builder.Append(" accessibility_text_scale_visual_sync_publishes=").Append(summary.AccessibilityTextScaleVisualSyncPublishes);
            builder.Append(" accessibility_text_scale_finite_guards=").Append(summary.AccessibilityTextScaleFiniteGuards);
            builder.Append(" settings_manager_text_scale_persistence=").Append(summary.SettingsManagerTextScalePersistence);
            builder.Append(" settings_manager_text_scale_applies=").Append(summary.SettingsManagerTextScaleApplies);
            builder.Append(" settings_manager_text_scale_finite_guards=").Append(summary.SettingsManagerTextScaleFiniteGuards);
            builder.Append(" settings_panel_text_scale_controls=").Append(summary.SettingsPanelTextScaleControls);
            builder.Append(" settings_panel_text_scale_bindings=").Append(summary.SettingsPanelTextScaleBindings);
            builder.Append(" settings_panel_text_scale_persistence=").Append(summary.SettingsPanelTextScalePersistence);
            builder.Append(" settings_panel_text_scale_zero_gc_labels=").Append(summary.SettingsPanelTextScaleZeroGcLabels);
            builder.Append(" settings_panel_text_scale_string_writes=").Append(summary.SettingsPanelTextScaleStringWrites);
            builder.Append(" accessibility_ui_motion_fields=").Append(summary.AccessibilityUiMotionFields);
            builder.Append(" accessibility_ui_motion_setters=").Append(summary.AccessibilityUiMotionSetters);
            builder.Append(" accessibility_ui_motion_visual_sync_publishes=").Append(summary.AccessibilityUiMotionVisualSyncPublishes);
            builder.Append(" accessibility_ui_motion_finite_guards=").Append(summary.AccessibilityUiMotionFiniteGuards);
            builder.Append(" settings_manager_ui_motion_persistence=").Append(summary.SettingsManagerUiMotionPersistence);
            builder.Append(" settings_manager_ui_motion_applies=").Append(summary.SettingsManagerUiMotionApplies);
            builder.Append(" settings_manager_ui_motion_finite_guards=").Append(summary.SettingsManagerUiMotionFiniteGuards);
            builder.Append(" settings_panel_ui_motion_controls=").Append(summary.SettingsPanelUiMotionControls);
            builder.Append(" settings_panel_ui_motion_bindings=").Append(summary.SettingsPanelUiMotionBindings);
            builder.Append(" settings_panel_ui_motion_persistence=").Append(summary.SettingsPanelUiMotionPersistence);
            builder.Append(" settings_panel_ui_motion_zero_gc_labels=").Append(summary.SettingsPanelUiMotionZeroGcLabels);
            builder.Append(" settings_panel_ui_motion_string_writes=").Append(summary.SettingsPanelUiMotionStringWrites);
            builder.Append(" ui_screen_shake_motion_scale_route=").Append(summary.UiScreenShakeMotionScaleRoute);
            builder.Append(" ui_screen_shake_motion_finite_guards=").Append(summary.UiScreenShakeMotionFiniteGuards);
            builder.Append(" ui_screen_shake_late_frame_writes=").Append(summary.UiScreenShakeLateFrameWrites);
            builder.Append(" ui_rescale_layout_snapshot_reads=").Append(summary.UiRescaleLayoutSnapshotReads);
            builder.Append(" ui_rescale_layout_legacy_consumes=").Append(summary.UiRescaleLayoutLegacyConsumes);
            builder.Append(" ui_rescale_layout_dedup_fields=").Append(summary.UiRescaleLayoutDedupFields);
            builder.Append(" ui_rescale_layout_reset_clears=").Append(summary.UiRescaleLayoutResetClears);
            builder.Append(" ui_rescale_layout_rebuild_calls=").Append(summary.UiRescaleLayoutRebuildCalls);
            builder.Append(" pda_corrupted_writers=").Append(summary.PdaCorruptedWriterDefinitions);
            builder.Append(" pda_corrupted_lateframe_calls=").Append(summary.PdaCorruptedLateFrameCalls);
            builder.Append(" pda_corrupted_span_writes=").Append(summary.PdaCorruptedBodySpanWrites);
            builder.Append(" pda_runtime_tmp_string_writes=").Append(summary.PdaRuntimeTmpStringWrites);
            builder.Append(" pda_finite_quality_resolvers=").Append(summary.PdaFiniteQualityResolvers);
            builder.Append(" pda_finite_quality_calls=").Append(summary.PdaFiniteQualityCalls);
            builder.Append(" pda_raw_quality_saturates=").Append(summary.PdaRawQualitySaturates);
            builder.Append(" pda_finite_quality_guards=").Append(summary.PdaFiniteQualityGuards);
            builder.Append(" pda_instant_reveal_contracts=").Append(summary.PdaInstantRevealContracts);
            builder.Append(" pda_instant_reveal_lifecycle_clears=").Append(summary.PdaInstantRevealLifecycleClears);
            builder.Append(" pda_ui_rescale_cold_init=").Append(summary.PdaUiRescaleColdInitializers);
            builder.Append(" pda_ui_rescale_lateframe_calls=").Append(summary.PdaUiRescaleLateFrameCalls);
            builder.Append(" pda_ui_rescale_snapshot_reads=").Append(summary.PdaUiRescaleSnapshotReads);
            builder.Append(" pda_ui_rescale_finite_guards=").Append(summary.PdaUiRescaleFiniteGuards);
            builder.Append(" pda_ui_rescale_font_applies=").Append(summary.PdaUiRescaleFontApplies);
            builder.Append(" audio_glitch_dto_defs=").Append(summary.AudioGlitchDtoDefinitions);
            builder.Append(" audio_glitch_overload_refs=").Append(summary.AudioGlitchPlaybackOverloads);
            builder.Append(" audio_glitch_resolvers=").Append(summary.AudioGlitchResolveMethods);
            builder.Append(" audio_glitch_quality_reads=").Append(summary.AudioGlitchQualityWeightReads);
            builder.Append(" audio_glitch_sanitizers=").Append(summary.AudioGlitchSanitizers);
            builder.Append(" audio_glitch_enqueue_sanitize_calls=").Append(summary.AudioGlitchEnqueueSanitizeCalls);
            builder.Append(" audio_glitch_duration_guards=").Append(summary.AudioGlitchDurationGuards);
            builder.Append(" audio_glitch_finite_guards=").Append(summary.AudioGlitchFiniteGuards);
            builder.Append(" audio_glitch_lateframe_flushes=").Append(summary.AudioGlitchLateFrameFlushes);
            builder.Append(" audio_glitch_dto_transfers=").Append(summary.AudioGlitchPendingDtoTransfers);
            builder.Append(" audio_glitch_playback_starts=").Append(summary.AudioGlitchPlaybackStarts);
            builder.Append(" audio_glitch_visual_sync_calls=").Append(summary.AudioGlitchVisualSyncCalls);
            builder.Append(" audio_glitch_stop_cancels_pending=").Append(summary.AudioGlitchStopCancelsPending);
            builder.Append(" audio_glitch_playback_state_writes=").Append(summary.AudioGlitchPlaybackStateWrites);
            builder.Append(" subtitle_audio_log_ring_defs=").Append(summary.SubtitleAudioLogPendingRingDefinitions);
            builder.Append(" subtitle_audio_log_callback_queues=").Append(summary.SubtitleAudioLogCallbackQueues);
            builder.Append(" subtitle_audio_log_direct_callback_calls=").Append(summary.SubtitleAudioLogCallbackDirectPresentationCalls);
            builder.Append(" subtitle_audio_log_lateframe_drains=").Append(summary.SubtitleAudioLogLateFrameDrains);
            builder.Append(" subtitle_audio_log_visual_dispatches=").Append(summary.SubtitleAudioLogVisualSyncDispatches);
            builder.Append(" subtitle_audio_log_lifecycle_clears=").Append(summary.SubtitleAudioLogLifecycleClears);
            builder.Append(" subtitle_audio_log_duration_guards=").Append(summary.SubtitleAudioLogDurationGuards);
            builder.Append(" terminal_os_dump_thread_primitives=").Append(summary.TerminalOsDumpThreadPrimitives);
            builder.Append(" terminal_os_dump_sync_drains=").Append(summary.TerminalOsDumpSynchronousDrains);
            builder.Append(" terminal_os_dump_return_drains=").Append(summary.TerminalOsDumpDrainReturnRoutes);
            builder.Append(" terminal_os_dump_bool_drains=").Append(summary.TerminalOsDumpBooleanDrains);
            builder.Append(" terminal_os_dump_bool_writes=").Append(summary.TerminalOsDumpBooleanWrites);
            builder.Append(" terminal_os_dump_context_warnings=").Append(summary.TerminalOsDumpContextWarnings);
            builder.Append(" terminal_os_dump_gate_locks=").Append(summary.TerminalOsDumpGateLockScopes);
            builder.Append(" terminal_os_dump_writes_after_lock=").Append(summary.TerminalOsDumpWritesAfterGateLock);
            builder.Append(" scene_world_bytes=").Append(summary.SceneWorldBytes);
            builder.Append(" scene_world_roots=").Append(summary.SceneWorldRoots);
            builder.Append(" scene_world_mapmagic_rows=").Append(summary.SceneWorldMapMagicRows);
            builder.Append(" scene_world_terrain_rows=").Append(summary.SceneWorldTerrainRows);
            builder.Append(" scene_world_terrain_collider_rows=").Append(summary.SceneWorldTerrainColliderRows);
            builder.Append(" scene_world_crest_markers=").Append(summary.SceneWorldCrestMarkers);
            builder.Append(" scene_world_ocean_prefab_assets=").Append(summary.SceneWorldOceanPrefabAssets);
            builder.Append(" scene_world_ocean_prefab_refs=").Append(summary.SceneWorldOceanPrefabRefs);
            builder.Append(" scene_world_dependency_warnings=").Append(summary.SceneWorldDependencyWarnings);
            builder.Append(" runtime_struct_layouts_checked=").Append(summary.RuntimeStructLayoutsChecked);
            builder.Append(" runtime_struct_literal_size_aligned=").Append(summary.RuntimeStructLiteralSizeAligned);
            builder.Append(" runtime_struct_literal_size_unaligned=").Append(summary.RuntimeStructLiteralSizeUnaligned);
            builder.Append(" runtime_struct_pack_one_findings=").Append(summary.RuntimeStructPackOneFindings);
            builder.Append(" runtime_struct_sizeof_refs=").Append(summary.RuntimeStructSizeofReferences);
            builder.Append(" meta_files_scanned=").Append(summary.MetaFilesScanned);
            builder.Append(" orphan_meta_files=").Append(summary.OrphanMetaFiles);
            builder.Append(" source_meta_files_scanned=").Append(summary.SourceFilesRequiringMetaScanned);
            builder.Append(" missing_source_meta_files=").Append(summary.MissingSourceMetaFiles);
            builder.Append(" dependency_findings=").Append(summary.DependencyFindings);
            builder.Append(" phase_findings=").Append(summary.PhaseFindings);
            builder.Append(" zero_gc_findings=").Append(summary.ZeroGcFindings);
            builder.Append(" job_complete_findings=").Append(summary.JobCompleteFindings);
            builder.Append(" lock_findings=").Append(summary.LockFindings);
            builder.Append(" prologue_blackbox_write_locks=").Append(summary.PrologueBlackBoxWriteLocksChecked);
            builder.Append(" prologue_blackbox_release_finally=").Append(summary.PrologueBlackBoxReleaseFinallyProofs);
            builder.Append(" prologue_blackbox_hoisted_telemetry=").Append(summary.PrologueBlackBoxHoistedTelemetryProofs);
            builder.Append(" prologue_blackbox_heavy_inside_lock=").Append(summary.PrologueBlackBoxHeavyInsideWriteLock);
            builder.Append(" pda_telemetry_write_locks=").Append(summary.PdaTelemetryWriteLocksChecked);
            builder.Append(" pda_telemetry_release_finally=").Append(summary.PdaTelemetryReleaseFinallyProofs);
            builder.Append(" pda_telemetry_redundant_readonly=").Append(summary.PdaTelemetryReadOnlyTelemetrySnapshots);
            builder.Append(" pda_telemetry_write_size_proofs=").Append(summary.PdaTelemetryWriteLockSizeProofs);
            builder.Append(" pda_telemetry_runtime_fallback_reads=").Append(summary.PdaTelemetryRuntimeStateFallbackReads);
            builder.Append(" pda_telemetry_streaming_snapshot_passes=").Append(summary.PdaTelemetryStreamingSnapshotPasses);
            builder.Append(" pda_blackbox_dump_single_snapshots=").Append(summary.PdaBlackBoxDumpSingleTelemetrySnapshots);
            builder.Append(" pda_blackbox_dump_per_row_reads=").Append(summary.PdaBlackBoxDumpPerRowTelemetryReads);
            builder.Append(" pda_blackbox_dump_transient_payloads=").Append(summary.PdaBlackBoxDumpTransientPayloads);
            builder.Append(" pda_blackbox_dump_raw_payload_allocs=").Append(summary.PdaBlackBoxDumpRawPayloadAllocs);
            builder.Append(" terminal_os_telemetry_layout_hoists=").Append(summary.TerminalOsTelemetryLayoutHashHoists);
            builder.Append(" terminal_os_telemetry_ring_after_snapshots=").Append(summary.TerminalOsTelemetryRingOpenAfterSnapshots);
            builder.Append(" terminal_os_telemetry_ring_length_guards=").Append(summary.TerminalOsTelemetryRingLengthGuards);
            builder.Append(" terminal_os_decryption_snapshot_before_ring=").Append(summary.TerminalOsDecryptionTelemetrySnapshotBeforeRing);
            builder.Append(" terminal_os_decryption_cursor_clamps=").Append(summary.TerminalOsDecryptionTelemetryCursorClamps);
            builder.Append(" terminal_os_input_faults_before_ring=").Append(summary.TerminalOsInputTelemetryFaultsBeforeRing);
            builder.Append(" terminal_os_input_cursor_clamps=").Append(summary.TerminalOsInputTelemetryCursorClamps);
            builder.Append(" data_vault_write_locks_checked=").Append(summary.DataVaultWriteLocksChecked);
            builder.Append(" data_vault_write_lock_helpers_checked=").Append(summary.DataVaultWriteLockHelpersChecked);
            builder.Append(" data_vault_write_lock_helper_callers_checked=").Append(summary.DataVaultWriteLockHelperCallersChecked);
            builder.Append(" data_vault_owner_write_scopes_checked=").Append(summary.DataVaultOwnerWriteScopesChecked);
            builder.Append(" gpu_locks_checked=").Append(summary.GpuWriteLocksChecked);
            builder.Append(" gpu_unlock_finally_checked=").Append(summary.GpuWriteUnlockFinallyChecked);
            builder.Append(" fatal_findings=").Append(summary.FatalFindings);
            builder.Append(" build_invocations=0");
            builder.Append(" analysis=RoslynAST_in_memory");
            return builder.ToString();
        }

        private static string BuildMethodInstanceKey(string owner, MethodDeclarationSyntax method)
        {
            return owner + "." + method.Identifier.ValueText + "@" + RelativePath(method) + ":" + LineOf(method);
        }

        private static string ResolveProjectRoot()
        {
            DirectoryInfo parent = Directory.GetParent(Application.dataPath);
            return parent == null ? string.Empty : parent.FullName;
        }

        private static string ToProjectRelativePath(string projectRoot, string fullPath)
        {
            string path = fullPath ?? string.Empty;
            if (!string.IsNullOrEmpty(projectRoot) &&
                path.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            return path.Replace('\\', '/');
        }

        private static string RelativePath(SyntaxNode node)
        {
            string path = node.SyntaxTree.FilePath;
            string root = ResolveProjectRoot();
            if (!string.IsNullOrEmpty(root) && !string.IsNullOrEmpty(path) && path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                path = path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return path.Replace('\\', '/');
        }

        private static int LineOf(SyntaxNode node)
        {
            return node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        }

        private static string Trim(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            value = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return value.Length <= 180 ? value : value.Substring(0, 180);
        }

        public struct ApexSummary
        {
            public int FilesExpected;
            public int FilesParsed;
            public int MissingFiles;
            public int ParseFailures;
            public int MethodsIndexed;
            public int HotRootsScanned;
            public int MethodsVisitedFromHotRoots;
            public int SyntaxNodesScanned;
            public int AppliedLoreBoundaryFilesScanned;
            public int AppliedLoreBoundaryCandidateFilesParsed;
            public int AppliedLoreUnlockFilesScanned;
            public int AppliedLoreUnlockCandidateFilesParsed;
            public int ScannerLoreFragmentSignalLayout;
            public int ScannerLoreFragmentCompletionPublishes;
            public int ScannerLoreFragmentCompletionFields;
            public int ScannerLoreFragmentPairedScanComplete;
            public int ScannerLoreFragmentPdaSnapshotReads;
            public int ScannerLoreFragmentPdaAupReads;
            public int ScannerLoreFragmentPdaScanCompleteAupFiniteChecks;
            public int ScannerLoreFragmentPdaUnlockCalls;
            public int ScannerLoreFragmentPdaPairedDedupes;
            public int ScannerLoreFragmentAppliedLoreAupPublishes;
            public int ScannerLoreFragmentAppliedLorePairedFlags;
            public int ScannerLoreFragmentHashOnlyFlagStrips;
            public int ScannerLoreFragmentScanEventsColdPrewarm;
            public int ScannerLoreFragmentLegacyDirectDequeues;
            public int ScannerLoreFragmentAllowedDirectPublishes;
            public int TerminalOsExpectedTerminals;
            public int TerminalOsRuntimeRows;
            public int TerminalOsRendererSlots;
            public int TerminalOsTransformSlots;
            public int TerminalOsVerifiedSlots;
            public int TerminalOsPreviewHashPairs;
            public int TerminalOsPreviewHashMismatches;
            public int TerminalOsPreviewHashDuplicateIndices;
            public int TerminalOsSceneBindingWarnings;
            public int TerminalPreviewSignalDefinitions;
            public int TerminalPreviewSignalLifecycleSizeProofs;
            public int AppliedLoreRuntimeLayoutProofs;
            public int AppliedLoreBootLayoutGuards;
            public int TerminalPreviewSignalBusContracts;
            public int TerminalPreviewPublisherCalls;
            public int TerminalPreviewSnapshotReads;
            public int TerminalPreviewLateFrameCalls;
            public int TerminalPreviewPublicWriterDefinitions;
            public int TerminalPreviewExternalWriterCalls;
            public int TerminalOsGraphicsRebuildLateFrameCalls;
            public int TerminalOsGraphicsRebuildSlowTickCalls;
            public int TerminalOsGraphicsRebuildJobGuards;
            public int TerminalOsQualityRuntimeRebuildGuards;
            public int TerminalOsQualityPlayingTextureBlocks;
            public int ScannableFragmentHashUnlocks;
            public int ScannableFragmentLateFrameEventFlushes;
            public int ScannableFragmentLifecycleClears;
            public int ScannableFragmentPendingStringClears;
            public int ScannableFragmentLockStateOrder;
            public int ScannableFragmentEventFlushBeforeDisable;
            public int NarrativeDiscoveryLoreHashCaches;
            public int NarrativeDiscoveryCachedUnlockCalls;
            public int NarrativeDiscoveryInteractionStringHashes;
            public int HectonDirectorPoiHashCaches;
            public int HectonDirectorPoiCachedDispatches;
            public int HectonDirectorPoiRuntimeStringHashes;
            public int AppliedLoreWorldImpactTickDrains;
            public int AppliedLoreWorldImpactLateFrameDrains;
            public int AppliedLoreWorldImpactQueuedAudioTransfers;
            public int AppliedLoreWorldImpactLifecycleClears;
            public int AppliedLoreWorldImpactSignalPublishes;
            public int AppliedLoreWorldImpactDedupGuards;
            public int AppliedLoreWorldImpactLayoutSizeConstants;
            public int AppliedLoreWorldImpactLayoutPaddingFields;
            public int AppliedLoreWorldImpactLayoutSizeofProofs;
            public int AppliedLoreWorldImpactCentralAuditProofs;
            public int AppliedLoreUtf8PassByRefProofs;
            public int AppliedLoreUtf8FacadeDuplicateSelectors;
            public int MetaCampaignVisualQueueCalls;
            public int MetaCampaignVisualFlushLateFrameCalls;
            public int MetaCampaignVisualPublishCalls;
            public int MetaCampaignVisualShaderWrites;
            public int MetaCampaignAudioQueueCalls;
            public int MetaCampaignAudioFlushLateFrameCalls;
            public int MetaCampaignAudioPublishCalls;
            public int MetaCampaignCartographyQueueCalls;
            public int MetaCampaignCartographyFlushLateFrameCalls;
            public int MetaCampaignCartographyPublishCalls;
            public int MessageTerminalFiniteTimeGuards;
            public int MessageTerminalPresentationScalarGuards;
            public int MessageTerminalPendingEventClears;
            public int MessageTerminalMessageHashFields;
            public int MessageTerminalMessageHashColdCaches;
            public int MessageTerminalHashEventQueues;
            public int MessageTerminalHashEventFlushes;
            public int MessageTerminalHashEventClears;
            public int MessageTerminalHashPendingReads;
            public int MessageTerminalLegacyPendingContains;
            public int UiRescaleProducerPublicRequests;
            public int UiRescaleProducerReasons;
            public int UiRescaleProducerFiniteGuards;
            public int UiRescaleProducerSignalPushes;
            public int UiRescaleProducerSignalInitializers;
            public int UiRescaleProducerLayoutApplies;
            public int AccessibilityTextScaleFields;
            public int AccessibilityTextScalePublicSetters;
            public int AccessibilityTextScaleVisualSyncPublishes;
            public int AccessibilityTextScaleFiniteGuards;
            public int SettingsManagerTextScalePersistence;
            public int SettingsManagerTextScaleApplies;
            public int SettingsManagerTextScaleFiniteGuards;
            public int SettingsPanelTextScaleControls;
            public int SettingsPanelTextScaleBindings;
            public int SettingsPanelTextScalePersistence;
            public int SettingsPanelTextScaleZeroGcLabels;
            public int SettingsPanelTextScaleStringWrites;
            public int AccessibilityUiMotionFields;
            public int AccessibilityUiMotionSetters;
            public int AccessibilityUiMotionVisualSyncPublishes;
            public int AccessibilityUiMotionFiniteGuards;
            public int SettingsManagerUiMotionPersistence;
            public int SettingsManagerUiMotionApplies;
            public int SettingsManagerUiMotionFiniteGuards;
            public int SettingsPanelUiMotionControls;
            public int SettingsPanelUiMotionBindings;
            public int SettingsPanelUiMotionPersistence;
            public int SettingsPanelUiMotionZeroGcLabels;
            public int SettingsPanelUiMotionStringWrites;
            public int UiScreenShakeMotionScaleRoute;
            public int UiScreenShakeMotionFiniteGuards;
            public int UiScreenShakeLateFrameWrites;
            public int UiRescaleLayoutSnapshotReads;
            public int UiRescaleLayoutLegacyConsumes;
            public int UiRescaleLayoutDedupFields;
            public int UiRescaleLayoutResetClears;
            public int UiRescaleLayoutRebuildCalls;
            public int PdaCorruptedWriterDefinitions;
            public int PdaCorruptedLateFrameCalls;
            public int PdaCorruptedBodySpanWrites;
            public int PdaRuntimeTmpStringWrites;
            public int PdaFiniteQualityResolvers;
            public int PdaFiniteQualityCalls;
            public int PdaRawQualitySaturates;
            public int PdaFiniteQualityGuards;
            public int PdaInstantRevealContracts;
            public int PdaInstantRevealLifecycleClears;
            public int PdaUiRescaleColdInitializers;
            public int PdaUiRescaleLateFrameCalls;
            public int PdaUiRescaleSnapshotReads;
            public int PdaUiRescaleFiniteGuards;
            public int PdaUiRescaleFontApplies;
            public int AudioGlitchDtoDefinitions;
            public int AudioGlitchPlaybackOverloads;
            public int AudioGlitchResolveMethods;
            public int AudioGlitchQualityWeightReads;
            public int AudioGlitchSanitizers;
            public int AudioGlitchEnqueueSanitizeCalls;
            public int AudioGlitchDurationGuards;
            public int AudioGlitchFiniteGuards;
            public int AudioGlitchLateFrameFlushes;
            public int AudioGlitchPendingDtoTransfers;
            public int AudioGlitchPlaybackStarts;
            public int AudioGlitchVisualSyncCalls;
            public int AudioGlitchStopCancelsPending;
            public int AudioGlitchPlaybackStateWrites;
            public int SubtitleAudioLogPendingRingDefinitions;
            public int SubtitleAudioLogCallbackQueues;
            public int SubtitleAudioLogCallbackDirectPresentationCalls;
            public int SubtitleAudioLogLateFrameDrains;
            public int SubtitleAudioLogVisualSyncDispatches;
            public int SubtitleAudioLogLifecycleClears;
            public int SubtitleAudioLogDurationGuards;
            public int TerminalOsDumpThreadPrimitives;
            public int TerminalOsDumpSynchronousDrains;
            public int TerminalOsDumpDrainReturnRoutes;
            public int TerminalOsDumpBooleanDrains;
            public int TerminalOsDumpBooleanWrites;
            public int TerminalOsDumpContextWarnings;
            public int TerminalOsDumpGateLockScopes;
            public int TerminalOsDumpWritesAfterGateLock;
            public int SceneWorldBytes;
            public int SceneWorldRoots;
            public int SceneWorldMapMagicRows;
            public int SceneWorldTerrainRows;
            public int SceneWorldTerrainColliderRows;
            public int SceneWorldCrestMarkers;
            public int SceneWorldOceanPrefabAssets;
            public int SceneWorldOceanPrefabRefs;
            public int SceneWorldDependencyWarnings;
            public int RuntimeStructLayoutsChecked;
            public int RuntimeStructLiteralSizeAligned;
            public int RuntimeStructLiteralSizeUnaligned;
            public int RuntimeStructPackOneFindings;
            public int RuntimeStructSizeofReferences;
            public int MetaFilesScanned;
            public int OrphanMetaFiles;
            public int SourceFilesRequiringMetaScanned;
            public int MissingSourceMetaFiles;
            public int DependencyFindings;
            public int PhaseFindings;
            public int ZeroGcFindings;
            public int JobCompleteFindings;
            public int LockFindings;
            public int PrologueBlackBoxWriteLocksChecked;
            public int PrologueBlackBoxReleaseFinallyProofs;
            public int PrologueBlackBoxHoistedTelemetryProofs;
            public int PrologueBlackBoxHeavyInsideWriteLock;
            public int PdaTelemetryWriteLocksChecked;
            public int PdaTelemetryReleaseFinallyProofs;
            public int PdaTelemetryReadOnlyTelemetrySnapshots;
            public int PdaTelemetryWriteLockSizeProofs;
            public int PdaTelemetryRuntimeStateFallbackReads;
            public int PdaTelemetryStreamingSnapshotPasses;
            public int PdaBlackBoxDumpSingleTelemetrySnapshots;
            public int PdaBlackBoxDumpPerRowTelemetryReads;
            public int PdaBlackBoxDumpTransientPayloads;
            public int PdaBlackBoxDumpRawPayloadAllocs;
            public int TerminalOsTelemetryLayoutHashHoists;
            public int TerminalOsTelemetryRingOpenAfterSnapshots;
            public int TerminalOsTelemetryRingLengthGuards;
            public int TerminalOsDecryptionTelemetrySnapshotBeforeRing;
            public int TerminalOsDecryptionTelemetryCursorClamps;
            public int TerminalOsInputTelemetryFaultsBeforeRing;
            public int TerminalOsInputTelemetryCursorClamps;
            public int DataVaultWriteLocksChecked;
            public int DataVaultWriteLockHelpersChecked;
            public int DataVaultWriteLockHelperCallersChecked;
            public int DataVaultOwnerWriteScopesChecked;
            public int GpuWriteLocksChecked;
            public int GpuWriteUnlockFinallyChecked;
            public int FatalFindings;
            public int FindingsLogged;
        }

        private readonly struct FileUnit
        {
            public readonly string RelativePath;
            public readonly CompilationUnitSyntax Root;

            public FileUnit(string relativePath, CompilationUnitSyntax root)
            {
                RelativePath = relativePath;
                Root = root;
            }
        }

        private readonly struct Finding
        {
            private readonly string _path;
            private readonly int _line;
            private readonly string _rule;
            private readonly string _detail;

            public Finding(string path, int line, string rule, string detail)
            {
                _path = path ?? string.Empty;
                _line = line;
                _rule = rule ?? string.Empty;
                _detail = detail ?? string.Empty;
            }

            public string ToConsoleLine()
            {
                return _rule + " " + _path + ":" + _line + " " + _detail;
            }
        }
    }
}
#endif
