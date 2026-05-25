using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PresentationDecouplingAudit;

internal static class Program
{
    private const string Schema = "hecton8.presentation_decoupling_audit.v1";
    private const string DefaultOutput = "Docs/Reports/PRESENTATION_DECOUPLING_OPTIMIZATION_REPORT_X_004.json";

    private static readonly string[] SimulationRoots =
    {
        "Assets/_Project/Scripts/",
        "Assets/_Project/Scripts/AI/",
        "Assets/_Project/Scripts/Atmosphere/",
        "Assets/_Project/Scripts/Construction/",
        "Assets/_Project/Scripts/Environment/",
        "Assets/_Project/Scripts/Fauna/",
        "Assets/_Project/Scripts/Gameplay/",
        "Assets/_Project/Scripts/Habitat/",
        "Assets/_Project/Scripts/Logistics/",
        "Assets/_Project/Scripts/Physics/",
        "Assets/_Project/Scripts/Power/",
        "Assets/_Project/Scripts/Thermodynamics/",
        "Assets/_Project/Scripts/Vehicles/",
        "Assets/_Project/Scripts/World/"
    };

    private static readonly string[] PresentationRoots =
    {
        "Assets/_Project/Scripts/Animation/",
        "Assets/_Project/Scripts/Audio/",
        "Assets/_Project/Scripts/Graphics/",
        "Assets/_Project/Scripts/Lighting/",
        "Assets/_Project/Scripts/Narrative/Camera/",
        "Assets/_Project/Scripts/PDA/",
        "Assets/_Project/Scripts/Prologue/VFX/",
        "Assets/_Project/Scripts/Rendering/",
        "Assets/_Project/Scripts/UI/",
        "Assets/_Project/Scripts/Visor/",
        "Assets/_Project/Scripts/VFX/",
        "Assets/_Project/Scripts/Vehicles/VFX/"
    };

    private static readonly string[] PresentationNameNeedles =
    {
        "Biolum",
        "DiffusionVolume",
        "GPUScatter",
        "HLOD",
        "Impostor",
        "LandmarkRenderer",
        "Lens",
        "Material",
        "Particle",
        "PDA",
        "Renderer",
        "Shader",
        "Visor",
        "Vfx",
        "VFX",
        "Visual"
    };

    private static readonly string[] ForbiddenTypeNeedles =
    {
        "Material",
        "Renderer",
        "MeshRenderer",
        "SkinnedMeshRenderer",
        "ParticleSystem",
        "AudioSource",
        "Animator",
        "Canvas",
        "CanvasGroup",
        "TMP_Text",
        "TextMeshProUGUI",
        "Shader"
    };

    private static readonly string[] ForbiddenNamespaceNeedles =
    {
        "TMPro",
        "UnityEngine.UI",
        "UnityEngine.VFX",
        "UnityEngine.Rendering"
    };

    private static readonly string[] MutableVaultNeedles =
    {
        "TryWriteHandle",
        "ResolveWriteHandle",
        "ResolveWrite",
        "AcquireWrite",
        "TryAcquireWrite",
        "TryGetMutable",
        "GetMutable",
        "TryResolveMutable"
    };

    private static int Main(string[] args)
    {
        string repoRoot = ResolveRepoRoot(args);
        string sourceRoot = GetArg(args, "--root") ?? Path.Combine(repoRoot, "Assets", "_Project", "Scripts");
        string outputPath = GetArg(args, "--output") ?? Path.Combine(repoRoot, DefaultOutput);

        if (!Directory.Exists(sourceRoot))
        {
            Console.Error.WriteLine("Source root not found: " + sourceRoot);
            return 2;
        }

        List<Finding> findings = new(capacity: 4096);
        List<ParseFailure> parseFailures = new(capacity: 128);
        ScanSummary summary = new();

        string[] files = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < files.Length; i++)
            ScanFile(repoRoot, files[i], findings, parseFailures, ref summary);

        summary.ScannedFiles = files.Length;
        summary.ParseFailures = parseFailures.Count;
        CountFindingClasses(findings, ref summary);

        string canonical = BuildCanonicalHashInput(findings, parseFailures, summary);
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        string report = BuildJson(repoRoot, sourceRoot, outputPath, summary, findings, parseFailures, hash);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        File.WriteAllText(outputPath, report, new UTF8Encoding(false));

        Console.WriteLine(
            "Presentation decoupling audit: files=" + summary.ScannedFiles +
            ", runtimeFiles=" + summary.RuntimeFiles +
            ", simulationFiles=" + summary.SimulationFiles +
            ", presentationFiles=" + summary.PresentationFiles +
            ", fatalHotPath=" + summary.FatalHotPathFindings +
            ", boundaryLeaks=" + summary.BoundaryTypeLeaks +
            ", mutablePresentation=" + summary.PresentationMutableTruthFindings +
            ", parserFailures=" + summary.ParseFailures +
            ", hash=" + hash);

        return parseFailures.Count == 0 ? 0 : 1;
    }

    private static void ScanFile(
        string repoRoot,
        string file,
        List<Finding> findings,
        List<ParseFailure> parseFailures,
        ref ScanSummary summary)
    {
        string relativePath = ToProjectPath(repoRoot, file);
        string normalizedPath = NormalizePath(relativePath);
        if (IsEditorFile(normalizedPath))
        {
            summary.EditorFilesSkipped++;
            return;
        }

        bool presentationByName = IsPresentationNamedFile(normalizedPath);
        bool presentationByRoot = MatchesAnyRoot(normalizedPath, PresentationRoots);
        bool isPresentation = presentationByRoot || presentationByName;
        bool isSimulation = MatchesAnyRoot(normalizedPath, SimulationRoots) && !isPresentation;
        bool isUiPresentation = IsUiPresentationPath(normalizedPath);
        if (!isSimulation && !isPresentation)
            return;

        summary.RuntimeFiles++;
        if (isSimulation)
            summary.SimulationFiles++;
        if (isPresentation)
            summary.PresentationFiles++;

        string source;
        try
        {
            source = File.ReadAllText(file, Encoding.UTF8);
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            parseFailures.Add(new ParseFailure(normalizedPath, 0, exception.GetType().Name));
            return;
        }

        SyntaxTree tree = CSharpSyntaxTree.ParseText(source, path: file);
        CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
        foreach (Diagnostic diagnostic in tree.GetDiagnostics())
        {
            if (diagnostic.Severity != DiagnosticSeverity.Error)
                continue;

            FileLinePositionSpan span = diagnostic.Location.GetLineSpan();
            parseFailures.Add(new ParseFailure(normalizedPath, span.StartLinePosition.Line + 1, diagnostic.Id));
            return;
        }

        Dictionary<string, Dictionary<string, List<MethodDeclarationSyntax>>> methodLookup = BuildMethodLookup(root);

        if (isSimulation)
            ScanSimulationImports(normalizedPath, root, findings);

        foreach (SyntaxNode node in root.DescendantNodes())
        {
            summary.SyntaxNodesVisited++;
            if (isSimulation && node is FieldDeclarationSyntax field)
            {
                ScanSimulationField(normalizedPath, tree, field, findings);
            }
            else if (isSimulation && node is PropertyDeclarationSyntax property)
            {
                ScanSimulationProperty(normalizedPath, tree, property, findings);
            }
            else if (node is MethodDeclarationSyntax method)
            {
                ScanMethod(normalizedPath, tree, method, isSimulation, isPresentation, isUiPresentation, methodLookup, findings, ref summary);
            }
        }
    }

    private static void ScanSimulationImports(string path, CompilationUnitSyntax root, List<Finding> findings)
    {
        foreach (UsingDirectiveSyntax usingDirective in root.Usings)
        {
            string text = usingDirective.Name?.ToString() ?? string.Empty;
            for (int i = 0; i < ForbiddenNamespaceNeedles.Length; i++)
            {
                if (!text.Equals(ForbiddenNamespaceNeedles[i], StringComparison.Ordinal))
                    continue;

                int line = usingDirective.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                findings.Add(new Finding(
                    path,
                    line,
                    "SIMULATION_NAMESPACE_LEAK",
                    text,
                    "using",
                    ResolveNamespace(usingDirective),
                    ResolveTypeName(usingDirective),
                    string.Empty,
                    "Simulation assembly imports presentation namespace.",
                    BuildConversionPlan(text),
                    ResolveOwnerRoute(text, path),
                    "PRE_SIMULATION/SIMULATION forbidden"));
            }
        }
    }

    private static void ScanSimulationField(string path, SyntaxTree tree, FieldDeclarationSyntax field, List<Finding> findings)
    {
        string typeText = field.Declaration.Type.ToString();
        string token = ResolveForbiddenTypeToken(typeText);
        if (token.Length == 0)
            return;

        foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
        {
            findings.Add(new Finding(
                path,
                field.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                "BOUNDARY_TYPE_LEAK",
                token,
                "field",
                ResolveNamespace(field),
                ResolveTypeName(field),
                variable.Identifier.ValueText,
                "Simulation file owns a Unity presentation field.",
                BuildConversionPlan(token),
                ResolveOwnerRoute(token, path),
                "cold field still violates simulation blindness"));
        }
    }

    private static void ScanSimulationProperty(string path, SyntaxTree tree, PropertyDeclarationSyntax property, List<Finding> findings)
    {
        string typeText = property.Type.ToString();
        string token = ResolveForbiddenTypeToken(typeText);
        if (token.Length == 0)
            return;

        findings.Add(new Finding(
            path,
            property.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            "BOUNDARY_TYPE_LEAK",
            token,
            "property",
            ResolveNamespace(property),
            ResolveTypeName(property),
            property.Identifier.ValueText,
            "Simulation file exposes a Unity presentation property.",
            BuildConversionPlan(token),
            ResolveOwnerRoute(token, path),
            "cold property still violates simulation blindness"));
    }

    private static void ScanMethod(
        string path,
        SyntaxTree tree,
        MethodDeclarationSyntax method,
        bool isSimulation,
        bool isPresentation,
        bool isUiPresentation,
        Dictionary<string, Dictionary<string, List<MethodDeclarationSyntax>>> methodLookup,
        List<Finding> findings,
        ref ScanSummary summary)
    {
        string methodName = method.Identifier.ValueText;
        string phase = ResolvePhase(method);
        bool hotPreVisual = IsPreVisualHotMethod(methodName) || IsBurstOrJobMethod(method);
        bool visualSync = IsVisualSyncMethod(methodName);

        if (isSimulation && hotPreVisual)
            summary.SimulationHotMethods++;
        if (isPresentation && visualSync)
            summary.PresentationVisualSyncMethods++;

        foreach (SyntaxNode node in method.DescendantNodes())
        {
            if (isSimulation && hotPreVisual && TryResolveForbiddenPresentationOperation(node, out string token, out string kind))
            {
                findings.Add(new Finding(
                    path,
                    node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    "FATAL_HOT_PATH_PRESENTATION_API",
                    token,
                    kind,
                    ResolveNamespace(method),
                    ResolveTypeName(method),
                    methodName,
                    "Presentation API used before VISUAL_SYNC in simulation-owned source.",
                    BuildConversionPlan(token),
                    ResolveOwnerRoute(token, path),
                    phase));
            }

            if (isPresentation && TryResolveMutableTruthOperation(node, out string mutableToken, out string mutableKind))
            {
                findings.Add(new Finding(
                    path,
                    node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    "PRESENTATION_MUTABLE_TRUTH_ACCESS",
                    mutableToken,
                    mutableKind,
                    ResolveNamespace(method),
                    ResolveTypeName(method),
                    methodName,
                    "Presentation source attempts mutable DataVault or write access.",
                    "Replace with TryReadHandle/TryReadOnlyHandle snapshot consumption and push commands through owner queues only when gameplay requires it.",
                    "Presentation owner must consume immutable snapshot in VISUAL_SYNC; write route belongs to simulation owner.",
                    phase));
            }

            if (isUiPresentation && IsHotPresentationMethod(methodName) && TryResolveUiStringAllocation(node, out string uiToken, out string uiKind))
            {
                findings.Add(new Finding(
                    path,
                    node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    "UI_STRING_GC_RISK",
                    uiToken,
                    uiKind,
                    ResolveNamespace(method),
                    ResolveTypeName(method),
                    methodName,
                    "Presentation hot path formats or assigns managed strings.",
                    BuildConversionPlan(uiToken),
                    ResolveOwnerRoute(uiToken, path),
                    phase));
            }
        }

        if (isSimulation && hotPreVisual)
        {
            ScanSameTypeHelperClosure(path, method, methodLookup, findings);
        }
    }

    private static Dictionary<string, Dictionary<string, List<MethodDeclarationSyntax>>> BuildMethodLookup(CompilationUnitSyntax root)
    {
        var lookup = new Dictionary<string, Dictionary<string, List<MethodDeclarationSyntax>>>(StringComparer.Ordinal);
        foreach (MethodDeclarationSyntax method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            string typeName = ResolveTypeName(method);
            if (!lookup.TryGetValue(typeName, out Dictionary<string, List<MethodDeclarationSyntax>>? byName))
            {
                byName = new Dictionary<string, List<MethodDeclarationSyntax>>(StringComparer.Ordinal);
                lookup.Add(typeName, byName);
            }

            string methodName = method.Identifier.ValueText;
            if (!byName.TryGetValue(methodName, out List<MethodDeclarationSyntax>? overloads))
            {
                overloads = new List<MethodDeclarationSyntax>();
                byName.Add(methodName, overloads);
            }

            overloads.Add(method);
        }

        return lookup;
    }

    private static void ScanSameTypeHelperClosure(
        string path,
        MethodDeclarationSyntax hotMethod,
        Dictionary<string, Dictionary<string, List<MethodDeclarationSyntax>>> methodLookup,
        List<Finding> findings)
    {
        string typeName = ResolveTypeName(hotMethod);
        if (!methodLookup.TryGetValue(typeName, out Dictionary<string, List<MethodDeclarationSyntax>>? byName))
            return;

        var visited = new HashSet<string>(StringComparer.Ordinal);
        string rootName = hotMethod.Identifier.ValueText;
        foreach (InvocationExpressionSyntax invocation in hotMethod.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (TryResolveSameTypeInvocation(invocation, typeName, out string helperName))
            {
                ScanHelperMethod(path, typeName, byName, helperName, rootName, visited, findings);
            }
        }
    }

    private static void ScanHelperMethod(
        string path,
        string typeName,
        Dictionary<string, List<MethodDeclarationSyntax>> byName,
        string methodName,
        string rootName,
        HashSet<string> visited,
        List<Finding> findings)
    {
        if (!byName.TryGetValue(methodName, out List<MethodDeclarationSyntax>? overloads))
            return;

        foreach (MethodDeclarationSyntax helper in overloads)
        {
            string key = typeName + "." + methodName + ":" + helper.GetLocation().GetLineSpan().StartLinePosition.Line;
            if (!visited.Add(key))
                continue;

            foreach (SyntaxNode node in helper.DescendantNodes())
            {
                if (TryResolveForbiddenPresentationOperation(node, out string token, out string kind))
                {
                    findings.Add(new Finding(
                        path,
                        node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        "FATAL_HOT_PATH_PRESENTATION_API",
                        token,
                        "helper_" + kind,
                        ResolveNamespace(helper),
                        typeName,
                        rootName + "->" + methodName,
                        "Presentation API reached from a hot simulation method through a same-type helper.",
                        BuildConversionPlan(token),
                        ResolveOwnerRoute(token, path),
                        ResolvePhase(helper)));
                }
            }

            foreach (InvocationExpressionSyntax invocation in helper.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (TryResolveSameTypeInvocation(invocation, typeName, out string nestedHelper))
                {
                    ScanHelperMethod(path, typeName, byName, nestedHelper, rootName, visited, findings);
                }
            }
        }
    }

    private static bool TryResolveSameTypeInvocation(InvocationExpressionSyntax invocation, string typeName, out string methodName)
    {
        methodName = string.Empty;
        switch (invocation.Expression)
        {
            case IdentifierNameSyntax identifier:
                methodName = identifier.Identifier.ValueText;
                return true;
            case MemberAccessExpressionSyntax memberAccess when memberAccess.Expression is ThisExpressionSyntax:
                methodName = memberAccess.Name.Identifier.ValueText;
                return true;
            case MemberAccessExpressionSyntax memberAccess when memberAccess.Expression is IdentifierNameSyntax receiver && receiver.Identifier.ValueText == LastTypeName(typeName):
                methodName = memberAccess.Name.Identifier.ValueText;
                return true;
            default:
                return false;
        }
    }

    private static string LastTypeName(string typeName)
    {
        int index = typeName.LastIndexOf('.');
        return index < 0 ? typeName : typeName[(index + 1)..];
    }

    private static bool TryResolveForbiddenPresentationOperation(SyntaxNode node, out string token, out string kind)
    {
        token = string.Empty;
        kind = string.Empty;

        if (node is InvocationExpressionSyntax invocation)
            return TryResolveForbiddenInvocation(invocation, out token, out kind);

        if (node is AssignmentExpressionSyntax assignment)
            return TryResolveForbiddenAssignment(assignment, out token, out kind);

        if (node is ObjectCreationExpressionSyntax objectCreation)
        {
            string typeText = objectCreation.Type.ToString();
            token = ResolveForbiddenTypeToken(typeText);
            if (token.Length == 0)
                return false;

            kind = "new_presentation_object";
            return true;
        }

        return false;
    }

    private static bool TryResolveForbiddenInvocation(InvocationExpressionSyntax invocation, out string token, out string kind)
    {
        token = string.Empty;
        kind = "invocation";

        if (invocation.Expression is IdentifierNameSyntax identifier)
        {
            string name = identifier.Identifier.ValueText;
            if (name == "Instantiate")
            {
                token = "Object.Instantiate";
                return true;
            }
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        string member = memberAccess.Name.Identifier.ValueText;
        string expression = memberAccess.Expression.ToString();
        string lowerExpression = expression.ToLowerInvariant();

        if (expression == "Shader" && member.StartsWith("Set", StringComparison.Ordinal))
        {
            token = "Shader." + member;
            return true;
        }

        if ((expression == "Graphics" || expression == "UnityEngine.Graphics") && member.StartsWith("Draw", StringComparison.Ordinal))
        {
            token = "Graphics." + member;
            return true;
        }

        if (lowerExpression.Contains("commandbuffer", StringComparison.Ordinal) && member.StartsWith("SetGlobal", StringComparison.Ordinal))
        {
            token = "CommandBuffer." + member;
            return true;
        }

        if ((member == "SetData" || member == "LockBufferForWrite" || member == "UnlockBufferAfterWrite") &&
            (lowerExpression.Contains("buffer", StringComparison.Ordinal) ||
             lowerExpression.Contains("graphics", StringComparison.Ordinal) ||
             lowerExpression.Contains("compute", StringComparison.Ordinal)))
        {
            token = "GraphicsBuffer." + member;
            return true;
        }

        if (member == "UploadArraySetData")
        {
            token = "GraphicsBuffer.SetData";
            return true;
        }

        if (member == "ReadPixels")
        {
            token = "Texture2D.ReadPixels";
            return true;
        }

        if (member == "Dispatch" && lowerExpression.Contains("compute", StringComparison.Ordinal))
        {
            token = "ComputeShader.Dispatch";
            return true;
        }

        if ((member == "SetPixels" || member == "SetPixel" || member == "SetPixelData" || member == "Apply") &&
            lowerExpression.Contains("texture", StringComparison.Ordinal))
        {
            token = "Texture2D." + member;
            return true;
        }

        if (member.StartsWith("Set", StringComparison.Ordinal) &&
            (lowerExpression.Contains("material", StringComparison.Ordinal) ||
             lowerExpression.Contains("renderer", StringComparison.Ordinal)))
        {
            token = "Material." + member;
            return true;
        }

        if ((member == "Play" || member == "PlayOneShot" || member == "PlayDelayed" || member == "PlayScheduled" || member == "Stop") &&
            (expression == "AudioSource" || lowerExpression.Contains("audio", StringComparison.Ordinal) || lowerExpression.Contains("source", StringComparison.Ordinal)))
        {
            token = "AudioSource." + member;
            return true;
        }

        if ((member == "Emit" || member == "Play" || member == "Stop" || member == "Simulate") &&
            (expression == "ParticleSystem" || lowerExpression.Contains("particle", StringComparison.Ordinal)))
        {
            token = "ParticleSystem." + member;
            return true;
        }

        if ((member == "Rotate" || member == "RotateAround" || member == "Translate" || member == "LookAt" || member == "SetPositionAndRotation") &&
            IsTransformExpression(expression))
        {
            token = "Transform." + member;
            return true;
        }

        if ((member == "SetBool" || member == "SetFloat" || member == "SetInteger" || member == "SetTrigger" || member == "Play" || member == "CrossFade") &&
            lowerExpression.Contains("animator", StringComparison.Ordinal))
        {
            token = "Animator." + member;
            return true;
        }

        if (member == "SetActive" && lowerExpression.Contains("gameobject", StringComparison.Ordinal))
        {
            token = "GameObject.SetActive";
            return true;
        }

        if (member == "SetText" && IsTextExpression(lowerExpression))
        {
            token = "TMP_Text.SetText";
            return true;
        }

        return false;
    }

    private static bool TryResolveForbiddenAssignment(AssignmentExpressionSyntax assignment, out string token, out string kind)
    {
        token = string.Empty;
        kind = "assignment";
        string left = assignment.Left.ToString();
        string lowerLeft = left.ToLowerInvariant();

        if (left.EndsWith(".text", StringComparison.Ordinal) || lowerLeft.Contains("tmp_text.text", StringComparison.Ordinal))
        {
            token = "TMP_Text.text";
            return true;
        }

        int dot = left.LastIndexOf('.');
        if (dot > 0 &&
            (left.EndsWith(".position", StringComparison.Ordinal) ||
             left.EndsWith(".rotation", StringComparison.Ordinal) ||
             left.EndsWith(".localPosition", StringComparison.Ordinal) ||
             left.EndsWith(".localRotation", StringComparison.Ordinal) ||
             left.EndsWith(".localScale", StringComparison.Ordinal)) &&
            IsTransformExpression(left.Substring(0, dot)))
        {
            token = "Transform." + left[(dot + 1)..];
            return true;
        }

        if ((lowerLeft.Contains("material", StringComparison.Ordinal) || lowerLeft.Contains("renderer", StringComparison.Ordinal)) &&
            (left.EndsWith(".color", StringComparison.Ordinal) || left.EndsWith(".material", StringComparison.Ordinal) || left.EndsWith(".materials", StringComparison.Ordinal)))
        {
            token = "Renderer/Material mutation";
            return true;
        }

        if (lowerLeft.EndsWith(".enabled", StringComparison.Ordinal) &&
            (lowerLeft.Contains("canvas", StringComparison.Ordinal) || lowerLeft.Contains("renderer", StringComparison.Ordinal) || lowerLeft.Contains("particle", StringComparison.Ordinal)))
        {
            token = "Presentation.enabled";
            return true;
        }

        return false;
    }

    private static bool TryResolveMutableTruthOperation(SyntaxNode node, out string token, out string kind)
    {
        token = string.Empty;
        kind = string.Empty;
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        string text = ResolveInvocationMemberName(invocation);
        for (int i = 0; i < MutableVaultNeedles.Length; i++)
        {
            if (!text.Equals(MutableVaultNeedles[i], StringComparison.Ordinal))
                continue;

            token = MutableVaultNeedles[i];
            kind = "mutable_vault_invocation";
            return true;
        }

        return false;
    }

    private static string ResolveInvocationMemberName(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            return memberAccess.Name.Identifier.ValueText;

        if (invocation.Expression is IdentifierNameSyntax identifier)
            return identifier.Identifier.ValueText;

        return invocation.Expression.ToString();
    }

    private static bool TryResolveUiStringAllocation(SyntaxNode node, out string token, out string kind)
    {
        token = string.Empty;
        kind = string.Empty;
        if (node is AssignmentExpressionSyntax assignment)
        {
            string left = assignment.Left.ToString();
            if (left.EndsWith(".text", StringComparison.Ordinal))
            {
                token = "TMP_Text.text";
                kind = "text_assignment";
                return true;
            }
        }

        if (node is InterpolatedStringExpressionSyntax)
        {
            token = "interpolated_string";
            kind = "string_formatting";
            return true;
        }

        if (node is BinaryExpressionSyntax binary &&
            binary.IsKind(SyntaxKind.AddExpression) &&
            (ContainsStringLiteral(binary.Left) || ContainsStringLiteral(binary.Right)))
        {
            token = "string_concatenation";
            kind = "string_formatting";
            return true;
        }

        if (node is ObjectCreationExpressionSyntax objectCreation &&
            objectCreation.Type.ToString().Equals("string", StringComparison.Ordinal))
        {
            token = "new string";
            kind = "string_allocation";
            return true;
        }

        if (node is InvocationExpressionSyntax invocation &&
            invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            string member = memberAccess.Name.Identifier.ValueText;
            string expression = memberAccess.Expression.ToString();
            if ((expression == "string" || expression == "String") && (member == "Format" || member == "Concat"))
            {
                token = expression + "." + member;
                kind = "string_formatting";
                return true;
            }

            if (member == "AppendFormat")
            {
                token = "StringBuilder.AppendFormat";
                kind = "string_formatting";
                return true;
            }

            if (member == "ToString")
            {
                token = ".ToString";
                kind = "string_formatting";
                return true;
            }
        }

        return false;
    }

    private static bool ContainsStringLiteral(SyntaxNode node)
    {
        if (node is InterpolatedStringExpressionSyntax)
            return true;

        if (node is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
            return true;

        foreach (SyntaxNode descendant in node.DescendantNodes())
        {
            if (descendant is InterpolatedStringExpressionSyntax)
                return true;

            if (descendant is LiteralExpressionSyntax descendantLiteral && descendantLiteral.IsKind(SyntaxKind.StringLiteralExpression))
                return true;
        }

        return false;
    }

    private static string ResolveForbiddenTypeToken(string typeText)
    {
        for (int i = 0; i < ForbiddenTypeNeedles.Length; i++)
        {
            string token = ForbiddenTypeNeedles[i];
            if (ContainsWholeIdentifier(typeText, token))
                return token;
        }

        return string.Empty;
    }

    private static bool ContainsWholeIdentifier(string text, string token)
    {
        int start = 0;
        while (start < text.Length)
        {
            int index = text.IndexOf(token, start, StringComparison.Ordinal);
            if (index < 0)
                return false;

            int before = index - 1;
            int after = index + token.Length;
            bool beforeBoundary = before < 0 || !IsIdentifierChar(text[before]);
            bool afterBoundary = after >= text.Length || !IsIdentifierChar(text[after]);
            if (beforeBoundary && afterBoundary)
                return true;

            start = index + token.Length;
        }

        return false;
    }

    private static bool IsIdentifierChar(char value)
    {
        return (value >= 'a' && value <= 'z') ||
               (value >= 'A' && value <= 'Z') ||
               (value >= '0' && value <= '9') ||
               value == '_';
    }

    private static bool IsPreVisualHotMethod(string name)
    {
        return name is "Update" or "FixedUpdate" or "Tick" or "FixedTick" or "FastTick" or "SlowTick" or "ColdTick" or "FrostTick" or
            "PreSimulationTick" or "ScheduleSimulation" or "ScheduleFixedSimulation" or "PostSimulationTick" or "PostFixedSimulation" or "PostFixedTick" or
            "Execute";
    }

    private static bool IsVisualSyncMethod(string name)
    {
        return name is "LateFrameTick" or "LateUpdate" or "VisualSyncTick";
    }

    private static bool IsHotPresentationMethod(string name)
    {
        return IsVisualSyncMethod(name) || name is "Update" or "FixedUpdate" or "Tick" or "FastTick" or "SlowTick" or "Render";
    }

    private static bool IsBurstOrJobMethod(MethodDeclarationSyntax method)
    {
        if (method.Identifier.ValueText != "Execute")
            return HasBurstAttribute(method);

        TypeDeclarationSyntax? owner = method.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        string bases = owner?.BaseList?.ToString() ?? string.Empty;
        return bases.Contains("IJob", StringComparison.Ordinal) ||
               bases.Contains("IJobFor", StringComparison.Ordinal) ||
               bases.Contains("IJobParallelFor", StringComparison.Ordinal) ||
               owner?.Identifier.ValueText.EndsWith("Job", StringComparison.Ordinal) == true ||
               HasBurstAttribute(method) ||
               (owner != null && HasBurstAttribute(owner));
    }

    private static bool HasBurstAttribute(SyntaxNode node)
    {
        foreach (AttributeListSyntax list in node.ChildNodes().OfType<AttributeListSyntax>())
        {
            foreach (AttributeSyntax attribute in list.Attributes)
            {
                string name = attribute.Name.ToString();
                if (name.Contains("BurstCompile", StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }

    private static string ResolvePhase(MethodDeclarationSyntax method)
    {
        string name = method.Identifier.ValueText;
        if (name is "PreSimulationTick")
            return "PRE_SIMULATION";
        if (name is "ScheduleSimulation" or "ScheduleFixedSimulation" or "Execute" or "FixedTick" or "Tick" or "Update" or "FixedUpdate" or "FastTick" or "SlowTick" or "ColdTick" or "FrostTick")
            return "SIMULATION";
        if (name is "PostSimulationTick" or "PostFixedSimulation" or "PostFixedTick")
            return "POST_SIMULATION";
        if (IsVisualSyncMethod(name))
            return "VISUAL_SYNC";
        return "UNKNOWN";
    }

    private static bool IsTransformExpression(string expression)
    {
        string lower = expression.ToLowerInvariant();
        return expression == "transform" ||
               expression == "Transform" ||
               lower.EndsWith(".transform", StringComparison.Ordinal) ||
               lower.Contains("transform", StringComparison.Ordinal);
    }

    private static bool IsTextExpression(string lowerExpression)
    {
        return lowerExpression.Contains("text", StringComparison.Ordinal) ||
               lowerExpression.Contains("tmp", StringComparison.Ordinal) ||
               lowerExpression.Contains("label", StringComparison.Ordinal);
    }

    private static string BuildConversionPlan(string token)
    {
        if (token.Contains("AudioSource", StringComparison.Ordinal))
            return "Publish unmanaged audio intent from POST_SIMULATION to SignalBus<AudioSignal>; VISUAL_SYNC/DSP owner writes double-buffered ParamSnapshot. No AudioSource call in simulation.";
        if (token.Contains("TMP_Text", StringComparison.Ordinal) || token.Contains("string", StringComparison.Ordinal) || token.Contains("ToString", StringComparison.Ordinal))
            return "Keep gameplay value as DTO; UI LateFrameTick formats into preallocated char[]/Span<char> and submits TMP_Text.SetCharArray only when dirty.";
        if (token.Contains("ParticleSystem", StringComparison.Ordinal) || token.Contains("Instantiate", StringComparison.Ordinal))
            return "Replace spawned GameObject effect with pooled/GPU particle renderer or GraphicsBuffer scalar page consumed in VISUAL_SYNC.";
        if (token.Contains("Material", StringComparison.Ordinal) || token.Contains("Renderer", StringComparison.Ordinal) || token.Contains("Shader", StringComparison.Ordinal))
            return "Simulation writes scalar/bitmask DTO; VISUAL_SYNC batches dirty pages into GraphicsBuffer/constant buffer. GPU shader fakes glow, cracks, wobble, and color response.";
        if (token.Contains("Transform", StringComparison.Ordinal) || token.Contains("Animator", StringComparison.Ordinal))
            return "Simulation writes AUP pose/phase scalar; presentation owner applies transform/VAT/animator state in VISUAL_SYNC. Gameplay timers remain unmanaged DTOs.";
        if (token.Contains("Canvas", StringComparison.Ordinal) || token.Contains("Presentation.enabled", StringComparison.Ordinal))
            return "Simulation writes visibility/urgency bit; UI VISUAL_SYNC owns CanvasGroup alpha or shader state with hysteresis.";
        return "Route through immutable DTO or typed SignalBus lane; presentation consumes in VISUAL_SYNC only.";
    }

    private static string ResolveOwnerRoute(string token, string path)
    {
        if (token.Contains("AudioSource", StringComparison.Ordinal))
            return "Owner: Hecton8.Audio DSP/PlayerCriticalProceduralAudioRenderer; route: SignalBus<AudioSignal> or double-buffered DSP ParamSnapshot.";
        if (token.Contains("TMP_Text", StringComparison.Ordinal) || token.Contains("string", StringComparison.Ordinal) || token.Contains("ToString", StringComparison.Ordinal))
            return "Owner: Hecton8.UI late-frame HUD/text renderer; route: read-only DTO snapshot and fixed char buffer.";
        if (token.Contains("ParticleSystem", StringComparison.Ordinal) || token.Contains("Instantiate", StringComparison.Ordinal))
            return "Owner: Hecton8.VFX GPU renderer; route: unmanaged effect signal plus GraphicsBuffer dirty page.";
        if (token.Contains("Material", StringComparison.Ordinal) || token.Contains("Renderer", StringComparison.Ordinal) || token.Contains("Shader", StringComparison.Ordinal))
            return "Owner: Hecton8.Rendering/Hecton8.VFX VisualSync renderer; route: read-only vault handle and GraphicsBuffer/constant buffer upload.";
        if (token.Contains("Transform", StringComparison.Ordinal) || token.Contains("Animator", StringComparison.Ordinal))
            return "Owner: domain presentation adapter under Animation/VFX/UI; route: pose/phase DTO consumed by ILateFrameTickable.";
        if (token.Contains("Canvas", StringComparison.Ordinal))
            return "Owner: Hecton8.UI canvas presenter; route: immutable UI state snapshot.";
        return "Owner: simulation domain publishes immutable data; Echelon 8 consumes only in VISUAL_SYNC.";
    }

    private static void CountFindingClasses(List<Finding> findings, ref ScanSummary summary)
    {
        for (int i = 0; i < findings.Count; i++)
        {
            Finding finding = findings[i];
            if (finding.Classification == "FATAL_HOT_PATH_PRESENTATION_API")
                summary.FatalHotPathFindings++;
            else if (finding.Classification == "BOUNDARY_TYPE_LEAK")
                summary.BoundaryTypeLeaks++;
            else if (finding.Classification == "SIMULATION_NAMESPACE_LEAK")
                summary.NamespaceLeaks++;
            else if (finding.Classification == "PRESENTATION_MUTABLE_TRUTH_ACCESS")
                summary.PresentationMutableTruthFindings++;
            else if (finding.Classification == "UI_STRING_GC_RISK")
                summary.UiStringGcRisks++;
        }
    }

    private static string BuildCanonicalHashInput(List<Finding> findings, List<ParseFailure> parseFailures, ScanSummary summary)
    {
        StringBuilder builder = new();
        builder.Append(Schema).Append('|')
            .Append(summary.ScannedFiles).Append('|')
            .Append(summary.RuntimeFiles).Append('|')
            .Append(summary.FatalHotPathFindings).Append('|')
            .Append(summary.BoundaryTypeLeaks).Append('|')
            .Append(summary.PresentationMutableTruthFindings).Append('|')
            .Append(summary.UiStringGcRisks).Append('|')
            .Append(summary.ParseFailures).Append('\n');

        for (int i = 0; i < findings.Count; i++)
            builder.Append(findings[i].Path).Append('|').Append(findings[i].Line).Append('|').Append(findings[i].Classification).Append('|').Append(findings[i].Token).Append('\n');

        for (int i = 0; i < parseFailures.Count; i++)
            builder.Append("parse|").Append(parseFailures[i].Path).Append('|').Append(parseFailures[i].Line).Append('|').Append(parseFailures[i].Reason).Append('\n');

        return builder.ToString();
    }

    private static string BuildJson(
        string repoRoot,
        string sourceRoot,
        string outputPath,
        ScanSummary summary,
        List<Finding> findings,
        List<ParseFailure> parseFailures,
        string hash)
    {
        StringBuilder builder = new(capacity: Math.Max(8192, findings.Count * 768));
        builder.AppendLine("{");
        AppendProperty(builder, "schema", Schema, comma: true, indent: 2);
        AppendProperty(builder, "agent", "X_004", comma: true, indent: 2);
        AppendProperty(builder, "domain", "ECHELON 8: PRESENTATION & UX", comma: true, indent: 2);
        AppendProperty(builder, "role", "PRESENTATION_DECOUPLER_AND_VISUAL_SYNC_ENFORCER", comma: true, indent: 2);
        AppendProperty(builder, "sourceRoot", ToProjectPath(repoRoot, sourceRoot), comma: true, indent: 2);
        AppendProperty(builder, "outputPath", ToProjectPath(repoRoot, outputPath), comma: true, indent: 2);
        AppendProperty(builder, "scannerUsesRoslynAst", "true", comma: true, indent: 2, raw: true);
        AppendProperty(builder, "proofClass", "STATIC_ROSLYN_AST_ONLY_RUNTIME_PROFILER_PENDING", comma: true, indent: 2);
        AppendProperty(builder, "reportHashSha256", hash, comma: true, indent: 2);

        builder.AppendLine("  \"summary\": {");
        AppendProperty(builder, "scannedFiles", summary.ScannedFiles.ToString(), comma: true, indent: 4, raw: true);
        AppendProperty(builder, "runtimeFiles", summary.RuntimeFiles.ToString(), comma: true, indent: 4, raw: true);
        AppendProperty(builder, "editorFilesSkipped", summary.EditorFilesSkipped.ToString(), comma: true, indent: 4, raw: true);
        AppendProperty(builder, "simulationFiles", summary.SimulationFiles.ToString(), comma: true, indent: 4, raw: true);
        AppendProperty(builder, "presentationFiles", summary.PresentationFiles.ToString(), comma: true, indent: 4, raw: true);
        AppendProperty(builder, "simulationHotMethods", summary.SimulationHotMethods.ToString(), comma: true, indent: 4, raw: true);
        AppendProperty(builder, "presentationVisualSyncMethods", summary.PresentationVisualSyncMethods.ToString(), comma: true, indent: 4, raw: true);
        AppendProperty(builder, "fatalHotPathFindings", summary.FatalHotPathFindings.ToString(), comma: true, indent: 4, raw: true);
        AppendProperty(builder, "boundaryTypeLeaks", summary.BoundaryTypeLeaks.ToString(), comma: true, indent: 4, raw: true);
        AppendProperty(builder, "namespaceLeaks", summary.NamespaceLeaks.ToString(), comma: true, indent: 4, raw: true);
        AppendProperty(builder, "presentationMutableTruthFindings", summary.PresentationMutableTruthFindings.ToString(), comma: true, indent: 4, raw: true);
        AppendProperty(builder, "uiStringGcRisks", summary.UiStringGcRisks.ToString(), comma: true, indent: 4, raw: true);
        AppendProperty(builder, "syntaxNodesVisited", summary.SyntaxNodesVisited.ToString(), comma: true, indent: 4, raw: true);
        AppendProperty(builder, "parseFailures", summary.ParseFailures.ToString(), comma: false, indent: 4, raw: true);
        builder.AppendLine("  },");

        builder.AppendLine("  \"scannedSimulationRoots\": [");
        AppendStringArray(builder, SimulationRoots, indent: 4);
        builder.AppendLine("  ],");
        builder.AppendLine("  \"scannedPresentationRoots\": [");
        AppendStringArray(builder, PresentationRoots, indent: 4);
        builder.AppendLine("  ],");
        builder.AppendLine("  \"dearLiePolicy\": {");
        AppendProperty(builder, "simulationTruth", "unmanaged DTOs, typed signals, and DataVault snapshots only", comma: true, indent: 4);
        AppendProperty(builder, "presentationTruth", "VISUAL_SYNC/LateFrameTick read-only consumer applying GPU/audio/UI fakes", comma: true, indent: 4);
        AppendProperty(builder, "lowTier", "dirty-page scalar uploads, bounded UI cadence, DSP param snapshots, no spawned presentation objects", comma: true, indent: 4);
        AppendProperty(builder, "middleTier", "full snapshot cadence for nearby visible state with coalesced presentation lanes", comma: true, indent: 4);
        AppendProperty(builder, "highTier", "richer shader/VAT/particle detail in VISUAL_SYNC after simulation remains flat", comma: true, indent: 4);
        AppendProperty(builder, "ultraTier", "visual overkill through GPU shaders and DSP only; gameplay truth cost remains invariant", comma: false, indent: 4);
        builder.AppendLine("  },");
        builder.AppendLine("  \"stressHarnessPlan\": {");
        AppendProperty(builder, "status", "STATIC_PLAN_ONLY_RUNTIME_PENDING", comma: true, indent: 4);
        AppendProperty(builder, "method", "flood read-only presentation consumers with extreme DTO/signal values after simulation phase; verify SystemDispatcher SIMULATION timing stays flat and costs move to VISUAL_SYNC/GPU/DSP", comma: true, indent: 4);
        AppendProperty(builder, "failureGate", "any presentation write handle, job completion, scene search, spawned GameObject, AudioSource call, or TMP string assignment before VISUAL_SYNC is fatal", comma: false, indent: 4);
        builder.AppendLine("  },");

        builder.AppendLine("  \"findings\": [");
        for (int i = 0; i < findings.Count; i++)
        {
            AppendFinding(builder, findings[i], i < findings.Count - 1);
        }
        builder.AppendLine("  ],");

        builder.AppendLine("  \"parseFailures\": [");
        for (int i = 0; i < parseFailures.Count; i++)
        {
            ParseFailure failure = parseFailures[i];
            builder.Append("    { ");
            AppendInlineProperty(builder, "path", failure.Path, comma: true);
            AppendInlineProperty(builder, "line", failure.Line.ToString(), comma: true, raw: true);
            AppendInlineProperty(builder, "reason", failure.Reason, comma: false);
            builder.Append(" }");
            if (i < parseFailures.Count - 1)
                builder.Append(',');
            builder.AppendLine();
        }
        builder.AppendLine("  ],");

        builder.AppendLine("  \"verification\": {");
        AppendProperty(builder, "staticRoslynAst", parseFailures.Count == 0 ? "PASS" : "FAIL_PARSE_ERRORS", comma: true, indent: 4);
        AppendProperty(builder, "runtimeProfiler", "PENDING_VERIFICATION", comma: true, indent: 4);
        AppendProperty(builder, "gcMonitor", "PENDING_VERIFICATION", comma: true, indent: 4);
        AppendProperty(builder, "unityImport", "PENDING_VERIFICATION", comma: true, indent: 4);
        AppendProperty(builder, "compile", "PENDING_VERIFICATION", comma: false, indent: 4);
        builder.AppendLine("  }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void AppendFinding(StringBuilder builder, Finding finding, bool comma)
    {
        builder.AppendLine("    {");
        AppendProperty(builder, "path", finding.Path, comma: true, indent: 6);
        AppendProperty(builder, "line", finding.Line.ToString(), comma: true, indent: 6, raw: true);
        AppendProperty(builder, "classification", finding.Classification, comma: true, indent: 6);
        AppendProperty(builder, "token", finding.Token, comma: true, indent: 6);
        AppendProperty(builder, "syntaxKind", finding.SyntaxKind, comma: true, indent: 6);
        AppendProperty(builder, "namespace", finding.NamespaceName, comma: true, indent: 6);
        AppendProperty(builder, "type", finding.TypeName, comma: true, indent: 6);
        AppendProperty(builder, "member", finding.MemberName, comma: true, indent: 6);
        AppendProperty(builder, "phase", finding.Phase, comma: true, indent: 6);
        AppendProperty(builder, "reason", finding.Reason, comma: true, indent: 6);
        AppendProperty(builder, "dearLieConversionPlan", finding.ConversionPlan, comma: true, indent: 6);
        AppendProperty(builder, "visualSyncOwnerRoute", finding.OwnerRoute, comma: false, indent: 6);
        builder.Append("    }");
        if (comma)
            builder.Append(',');
        builder.AppendLine();
    }

    private static void AppendStringArray(StringBuilder builder, string[] values, int indent)
    {
        for (int i = 0; i < values.Length; i++)
        {
            builder.Append(' ', indent).Append('"').Append(Escape(values[i])).Append('"');
            if (i < values.Length - 1)
                builder.Append(',');
            builder.AppendLine();
        }
    }

    private static void AppendProperty(StringBuilder builder, string name, string value, bool comma, int indent, bool raw = false)
    {
        builder.Append(' ', indent).Append('"').Append(Escape(name)).Append("\": ");
        if (raw)
            builder.Append(value);
        else
            builder.Append('"').Append(Escape(value)).Append('"');
        if (comma)
            builder.Append(',');
        builder.AppendLine();
    }

    private static void AppendInlineProperty(StringBuilder builder, string name, string value, bool comma, bool raw = false)
    {
        builder.Append('"').Append(Escape(name)).Append("\": ");
        if (raw)
            builder.Append(value);
        else
            builder.Append('"').Append(Escape(value)).Append('"');
        if (comma)
            builder.Append(", ");
    }

    private static string ResolveNamespace(SyntaxNode node)
    {
        BaseNamespaceDeclarationSyntax? baseNamespace = node.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>();
        return baseNamespace?.Name.ToString() ?? string.Empty;
    }

    private static string ResolveTypeName(SyntaxNode node)
    {
        TypeDeclarationSyntax? type = node.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        return type?.Identifier.ValueText ?? string.Empty;
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static bool IsEditorFile(string normalizedPath)
    {
        return normalizedPath.Contains("/Editor/", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.EndsWith("Editor.cs", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Contains("/Tests/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesAnyRoot(string normalizedPath, string[] roots)
    {
        for (int i = 0; i < roots.Length; i++)
        {
            if (normalizedPath.StartsWith(roots[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsPresentationNamedFile(string normalizedPath)
    {
        string fileName = Path.GetFileNameWithoutExtension(normalizedPath);
        for (int i = 0; i < PresentationNameNeedles.Length; i++)
        {
            if (fileName.Contains(PresentationNameNeedles[i], StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool IsUiPresentationPath(string normalizedPath)
    {
        return normalizedPath.StartsWith("Assets/_Project/Scripts/UI/", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.StartsWith("Assets/_Project/Scripts/Visor/", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Contains("HUD", StringComparison.Ordinal) ||
               normalizedPath.Contains("PDA", StringComparison.Ordinal) ||
               normalizedPath.Contains("Hud", StringComparison.Ordinal) ||
               normalizedPath.Contains("Pda", StringComparison.Ordinal);
    }

    private static string ResolveRepoRoot(string[] args)
    {
        string? explicitRoot = GetArg(args, "--repo");
        if (!string.IsNullOrWhiteSpace(explicitRoot))
            return Path.GetFullPath(explicitRoot);

        string current = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(current, "Assets", "_Project")))
            {
                return current;
            }

            DirectoryInfo? parent = Directory.GetParent(current);
            if (parent == null)
                break;

            current = parent.FullName;
        }

        return Directory.GetCurrentDirectory();
    }

    private static string? GetArg(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }

    private static string ToProjectPath(string repoRoot, string path)
    {
        string fullRoot = Path.GetFullPath(repoRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(path);
        if (fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            return NormalizePath(fullPath[fullRoot.Length..]);

        return NormalizePath(path);
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        StringBuilder builder = new(value.Length + 8);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            switch (c)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (c < ' ')
                        builder.Append("\\u").Append(((int)c).ToString("x4"));
                    else
                        builder.Append(c);
                    break;
            }
        }

        return builder.ToString();
    }

    private struct ScanSummary
    {
        public int ScannedFiles;
        public int RuntimeFiles;
        public int EditorFilesSkipped;
        public int SimulationFiles;
        public int PresentationFiles;
        public int SimulationHotMethods;
        public int PresentationVisualSyncMethods;
        public int FatalHotPathFindings;
        public int BoundaryTypeLeaks;
        public int NamespaceLeaks;
        public int PresentationMutableTruthFindings;
        public int UiStringGcRisks;
        public int SyntaxNodesVisited;
        public int ParseFailures;
    }

    private readonly record struct Finding(
        string Path,
        int Line,
        string Classification,
        string Token,
        string SyntaxKind,
        string NamespaceName,
        string TypeName,
        string MemberName,
        string Reason,
        string ConversionPlan,
        string OwnerRoute,
        string Phase);

    private readonly record struct ParseFailure(string Path, int Line, string Reason);
}
