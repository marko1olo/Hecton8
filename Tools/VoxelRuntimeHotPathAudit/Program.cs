using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace VoxelRuntimeHotPathAudit;

internal static class Program
{
    private const string Schema = "hecton8.voxel_runtime_hotpath_audit.v1";

    private static readonly string[] LinqTokens =
    {
        ".Select(",
        ".Where(",
        ".OrderBy(",
        ".ThenBy(",
        ".GroupBy(",
        ".Any(",
        ".All(",
        ".First(",
        ".FirstOrDefault(",
        ".Single(",
        ".SingleOrDefault(",
        ".ToArray(",
        ".ToList(",
        ".Count("
    };

    private static readonly string[] DefaultTargets =
    {
        "Assets/_Project/Scripts/HectonVoxelVolume.cs",
        "Assets/_Project/Scripts/VoxelDeltaProcessor.cs",
        "Assets/_Project/Scripts/VoxelDeformationSmokeTester.cs",
        "Assets/_Project/Scripts/Core/Contracts/GroundRadarContracts.cs",
        "Assets/_Project/Scripts/Core/Memory/H8Memory.cs",
        "Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsContracts.cs",
        "Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs",
        "Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsGpuUploadDispatcher.cs"
    };

    private static int Main(string[] args)
    {
        string repoRoot = Path.GetFullPath(GetArg(args, "--repo") ?? Directory.GetCurrentDirectory());
        string outputPath = Path.GetFullPath(GetArg(args, "--output") ?? Path.Combine(repoRoot, "Docs", "Reports", "VOXEL_RUNTIME_HOTPATH_AUDIT_1304.json"));
        List<string> files = ResolveFiles(repoRoot, args);
        List<Finding> findings = new(capacity: 256);
        List<ParseFailure> parseFailures = new(capacity: 16);

        for (int i = 0; i < files.Count; i++)
            ScanFile(repoRoot, files[i], findings, parseFailures);

        Summary summary = BuildSummary(files.Count, findings, parseFailures);
        string canonical = BuildCanonical(summary, findings, parseFailures);
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        string json = BuildJson(repoRoot, outputPath, files, summary, findings, parseFailures, hash);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        File.WriteAllText(outputPath, json, new UTF8Encoding(false));
        Console.WriteLine(
            "Voxel runtime hotpath audit: files=" + summary.ScannedFiles +
            ", parseFailures=" + summary.ParseFailures +
            ", objectCreations=" + summary.ObjectCreations +
            ", managedRiskCreations=" + summary.ManagedRiskCreations +
            ", nativeTempJobAllocations=" + summary.NativeTempJobAllocations +
            ", nativePersistentAllocations=" + summary.NativePersistentAllocations +
            ", stringFormatCalls=" + summary.StringFormatCalls +
            ", toStringCalls=" + summary.ToStringCalls +
            ", linqCalls=" + summary.LinqCalls +
            ", foreachStatements=" + summary.ForeachStatements +
            ", interpolatedStrings=" + summary.InterpolatedStrings +
            ", stringConcatSuspects=" + summary.StringConcatSuspects +
            ", hash=" + hash);

        return parseFailures.Count == 0 ? 0 : 1;
    }

    private static List<string> ResolveFiles(string repoRoot, string[] args)
    {
        List<string> files = new(capacity: DefaultTargets.Length);
        for (int i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], "--file", StringComparison.Ordinal) || i + 1 >= args.Length)
                continue;

            string file = args[++i];
            files.Add(Path.GetFullPath(Path.IsPathRooted(file) ? file : Path.Combine(repoRoot, file)));
        }

        if (files.Count > 0)
            return files;

        for (int i = 0; i < DefaultTargets.Length; i++)
            files.Add(Path.GetFullPath(Path.Combine(repoRoot, DefaultTargets[i])));

        return files;
    }

    private static void ScanFile(string repoRoot, string file, List<Finding> findings, List<ParseFailure> parseFailures)
    {
        string relativePath = ToProjectPath(repoRoot, file);
        if (!File.Exists(file))
        {
            parseFailures.Add(new ParseFailure(relativePath, 0, "FILE_NOT_FOUND"));
            return;
        }

        string source = File.ReadAllText(file, Encoding.UTF8);
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source, path: file);
        CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
        foreach (Diagnostic diagnostic in tree.GetDiagnostics())
        {
            if (diagnostic.Severity != DiagnosticSeverity.Error)
                continue;

            FileLinePositionSpan span = diagnostic.Location.GetLineSpan();
            parseFailures.Add(new ParseFailure(relativePath, span.StartLinePosition.Line + 1, diagnostic.Id));
            return;
        }

        foreach (ObjectCreationExpressionSyntax creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            string typeText = creation.Type.ToString();
            AddCreationFinding(relativePath, creation, typeText, creation.ToString(), findings);
        }

        foreach (ImplicitObjectCreationExpressionSyntax creation in root.DescendantNodes().OfType<ImplicitObjectCreationExpressionSyntax>())
            AddFinding(relativePath, creation, "implicit_object_creation", "unknown", creation.ToString(), findings);

        foreach (InvocationExpressionSyntax invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            string text = invocation.Expression.ToString();
            if (text == "string.Format" || text == "String.Format")
                AddFinding(relativePath, invocation, "string_format_call", text, invocation.ToString(), findings);
            else if (text.EndsWith(".ToString", StringComparison.Ordinal) || text == "ToString")
                AddFinding(relativePath, invocation, "to_string_call", text, invocation.ToString(), findings);
            else if (text.StartsWith("Enumerable.", StringComparison.Ordinal) || ContainsAny(invocation.ToString(), LinqTokens))
                AddFinding(relativePath, invocation, "linq_call_suspect", text, invocation.ToString(), findings);
        }

        foreach (ForEachStatementSyntax statement in root.DescendantNodes().OfType<ForEachStatementSyntax>())
            AddFinding(relativePath, statement, "foreach_statement", statement.Type.ToString(), statement.Expression.ToString(), findings);

        foreach (QueryExpressionSyntax query in root.DescendantNodes().OfType<QueryExpressionSyntax>())
            AddFinding(relativePath, query, "linq_query_expression", "query", query.ToString(), findings);

        foreach (InterpolatedStringExpressionSyntax interpolated in root.DescendantNodes().OfType<InterpolatedStringExpressionSyntax>())
            AddFinding(relativePath, interpolated, "interpolated_string", "string", interpolated.ToString(), findings);

        foreach (BinaryExpressionSyntax binary in root.DescendantNodes().OfType<BinaryExpressionSyntax>())
        {
            if (!binary.IsKind(SyntaxKind.AddExpression))
                continue;

            if (binary.Left is LiteralExpressionSyntax leftLiteral && leftLiteral.IsKind(SyntaxKind.StringLiteralExpression) ||
                binary.Right is LiteralExpressionSyntax rightLiteral && rightLiteral.IsKind(SyntaxKind.StringLiteralExpression))
            {
                AddFinding(relativePath, binary, "string_concat_suspect", "operator+", binary.ToString(), findings);
            }
        }
    }

    private static void AddCreationFinding(
        string relativePath,
        SyntaxNode node,
        string typeText,
        string text,
        List<Finding> findings)
    {
        string kind = "object_creation";
        if (IsNativeCollection(typeText))
        {
            kind = text.Contains("Allocator.TempJob", StringComparison.Ordinal)
                ? "native_tempjob_allocation"
                : text.Contains("Allocator.Persistent", StringComparison.Ordinal)
                    ? "native_persistent_allocation"
                    : "native_allocation";
        }
        else if (IsKnownValueType(typeText))
        {
            kind = "value_type_creation";
        }

        AddFinding(relativePath, node, kind, typeText, text, findings);
    }

    private static bool IsNativeCollection(string typeText)
    {
        return typeText.StartsWith("NativeArray<", StringComparison.Ordinal) ||
               typeText.StartsWith("NativeList<", StringComparison.Ordinal) ||
               typeText.StartsWith("NativeQueue<", StringComparison.Ordinal) ||
               typeText.StartsWith("NativeHashMap<", StringComparison.Ordinal) ||
               typeText.StartsWith("NativeParallelHashMap<", StringComparison.Ordinal);
    }

    private static bool IsKnownValueType(string typeText)
    {
        return typeText == "int2" ||
               typeText == "int3" ||
               typeText == "int4" ||
               typeText == "uint2" ||
               typeText == "uint3" ||
               typeText == "uint4" ||
               typeText == "float2" ||
               typeText == "float3" ||
               typeText == "float4" ||
               typeText == "double2" ||
               typeText == "double3" ||
               typeText == "double4" ||
               typeText.EndsWith("DTO", StringComparison.Ordinal) ||
               typeText.EndsWith("Job", StringComparison.Ordinal) ||
               typeText.EndsWith("Handle", StringComparison.Ordinal) ||
               typeText.EndsWith("Header", StringComparison.Ordinal) ||
               typeText.EndsWith("Entry", StringComparison.Ordinal) ||
               typeText.EndsWith("State", StringComparison.Ordinal) ||
               typeText.EndsWith("Request", StringComparison.Ordinal) ||
               typeText.EndsWith("Event", StringComparison.Ordinal) ||
               typeText.EndsWith("Signal", StringComparison.Ordinal);
    }

    private static bool ContainsAny(string text, string[] needles)
    {
        for (int i = 0; i < needles.Length; i++)
        {
            if (text.Contains(needles[i], StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static void AddFinding(string path, SyntaxNode node, string kind, string detail, string snippet, List<Finding> findings)
    {
        FileLinePositionSpan span = node.GetLocation().GetLineSpan();
        findings.Add(new Finding(
            path,
            span.StartLinePosition.Line + 1,
            ResolveOwner(node),
            kind,
            detail,
            OneLine(snippet)));
    }

    private static string ResolveOwner(SyntaxNode node)
    {
        BaseMethodDeclarationSyntax? method = node.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>();
        if (method != null)
            return method switch
            {
                MethodDeclarationSyntax m => m.Identifier.ValueText,
                ConstructorDeclarationSyntax c => c.Identifier.ValueText,
                _ => method.Kind().ToString()
            };

        PropertyDeclarationSyntax? property = node.FirstAncestorOrSelf<PropertyDeclarationSyntax>();
        return property != null ? property.Identifier.ValueText : string.Empty;
    }

    private static string OneLine(string value)
    {
        string text = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return text.Length <= 240 ? text : text[..240];
    }

    private static Summary BuildSummary(int scannedFiles, List<Finding> findings, List<ParseFailure> parseFailures)
    {
        Summary summary = new()
        {
            ScannedFiles = scannedFiles,
            ParseFailures = parseFailures.Count,
            TotalFindings = findings.Count
        };

        for (int i = 0; i < findings.Count; i++)
        {
            switch (findings[i].Kind)
            {
                case "object_creation":
                    summary.ObjectCreations++;
                    summary.ManagedRiskCreations++;
                    break;
                case "implicit_object_creation":
                    summary.ObjectCreations++;
                    summary.ManagedRiskCreations++;
                    break;
                case "value_type_creation":
                    summary.ObjectCreations++;
                    summary.ValueTypeCreations++;
                    break;
                case "native_tempjob_allocation":
                    summary.ObjectCreations++;
                    summary.NativeTempJobAllocations++;
                    break;
                case "native_persistent_allocation":
                    summary.ObjectCreations++;
                    summary.NativePersistentAllocations++;
                    break;
                case "native_allocation":
                    summary.ObjectCreations++;
                    summary.NativeOtherAllocations++;
                    break;
                case "string_format_call":
                    summary.StringFormatCalls++;
                    break;
                case "to_string_call":
                    summary.ToStringCalls++;
                    break;
                case "linq_call_suspect":
                case "linq_query_expression":
                    summary.LinqCalls++;
                    break;
                case "foreach_statement":
                    summary.ForeachStatements++;
                    break;
                case "interpolated_string":
                    summary.InterpolatedStrings++;
                    break;
                case "string_concat_suspect":
                    summary.StringConcatSuspects++;
                    break;
            }
        }

        return summary;
    }

    private static string BuildCanonical(Summary summary, List<Finding> findings, List<ParseFailure> parseFailures)
    {
        StringBuilder builder = new();
        builder.Append(Schema).Append('|')
            .Append(summary.ScannedFiles).Append('|')
            .Append(summary.TotalFindings).Append('|')
            .Append(summary.ParseFailures).Append('\n');
        for (int i = 0; i < findings.Count; i++)
        {
            Finding finding = findings[i];
            builder.Append(finding.Path).Append(':')
                .Append(finding.Line).Append(':')
                .Append(finding.Owner).Append(':')
                .Append(finding.Kind).Append(':')
                .Append(finding.Detail).Append('\n');
        }

        for (int i = 0; i < parseFailures.Count; i++)
        {
            ParseFailure failure = parseFailures[i];
            builder.Append("parse:")
                .Append(failure.Path).Append(':')
                .Append(failure.Line).Append(':')
                .Append(failure.Code).Append('\n');
        }

        return builder.ToString();
    }

    private static string BuildJson(
        string repoRoot,
        string outputPath,
        List<string> files,
        Summary summary,
        List<Finding> findings,
        List<ParseFailure> parseFailures,
        string hash)
    {
        StringBuilder builder = new(capacity: 16384);
        builder.AppendLine("{");
        WriteProp(builder, 1, "schema", Schema, comma: true);
        WriteProp(builder, 1, "agentId", "1304", comma: true);
        WriteProp(builder, 1, "repoRoot", repoRoot, comma: true);
        WriteProp(builder, 1, "outputPath", outputPath, comma: true);
        WriteProp(builder, 1, "auditHashSha256", hash, comma: true);
        Indent(builder, 1).AppendLine("\"summary\": {");
        WriteProp(builder, 2, "scannedFiles", summary.ScannedFiles, comma: true);
        WriteProp(builder, 2, "parseFailures", summary.ParseFailures, comma: true);
        WriteProp(builder, 2, "totalFindings", summary.TotalFindings, comma: true);
        WriteProp(builder, 2, "objectCreations", summary.ObjectCreations, comma: true);
        WriteProp(builder, 2, "valueTypeCreations", summary.ValueTypeCreations, comma: true);
        WriteProp(builder, 2, "managedRiskCreations", summary.ManagedRiskCreations, comma: true);
        WriteProp(builder, 2, "nativeTempJobAllocations", summary.NativeTempJobAllocations, comma: true);
        WriteProp(builder, 2, "nativePersistentAllocations", summary.NativePersistentAllocations, comma: true);
        WriteProp(builder, 2, "nativeOtherAllocations", summary.NativeOtherAllocations, comma: true);
        WriteProp(builder, 2, "stringFormatCalls", summary.StringFormatCalls, comma: true);
        WriteProp(builder, 2, "toStringCalls", summary.ToStringCalls, comma: true);
        WriteProp(builder, 2, "linqCalls", summary.LinqCalls, comma: true);
        WriteProp(builder, 2, "foreachStatements", summary.ForeachStatements, comma: true);
        WriteProp(builder, 2, "interpolatedStrings", summary.InterpolatedStrings, comma: true);
        WriteProp(builder, 2, "stringConcatSuspects", summary.StringConcatSuspects, comma: false);
        Indent(builder, 1).AppendLine("},");
        WriteStringArray(builder, 1, "files", files, comma: true);
        WriteFindings(builder, findings, comma: true);
        WriteParseFailures(builder, parseFailures);
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void WriteStringArray(StringBuilder builder, int depth, string name, List<string> values, bool comma)
    {
        Indent(builder, depth).Append('"').Append(name).AppendLine("\": [");
        for (int i = 0; i < values.Count; i++)
        {
            Indent(builder, depth + 1).Append('"').Append(EscapeJson(values[i])).Append(i == values.Count - 1 ? "\"" : "\",").AppendLine();
        }

        Indent(builder, depth).Append(comma ? "]," : "]").AppendLine();
    }

    private static void WriteFindings(StringBuilder builder, List<Finding> findings, bool comma)
    {
        Indent(builder, 1).AppendLine("\"findings\": [");
        for (int i = 0; i < findings.Count; i++)
        {
            Finding finding = findings[i];
            Indent(builder, 2).AppendLine("{");
            WriteProp(builder, 3, "path", finding.Path, comma: true);
            WriteProp(builder, 3, "line", finding.Line, comma: true);
            WriteProp(builder, 3, "owner", finding.Owner, comma: true);
            WriteProp(builder, 3, "kind", finding.Kind, comma: true);
            WriteProp(builder, 3, "detail", finding.Detail, comma: true);
            WriteProp(builder, 3, "snippet", finding.Snippet, comma: false);
            Indent(builder, 2).Append(i == findings.Count - 1 ? "}" : "},").AppendLine();
        }

        Indent(builder, 1).Append(comma ? "]," : "]").AppendLine();
    }

    private static void WriteParseFailures(StringBuilder builder, List<ParseFailure> failures)
    {
        Indent(builder, 1).AppendLine("\"parseFailures\": [");
        for (int i = 0; i < failures.Count; i++)
        {
            ParseFailure failure = failures[i];
            Indent(builder, 2).AppendLine("{");
            WriteProp(builder, 3, "path", failure.Path, comma: true);
            WriteProp(builder, 3, "line", failure.Line, comma: true);
            WriteProp(builder, 3, "code", failure.Code, comma: false);
            Indent(builder, 2).Append(i == failures.Count - 1 ? "}" : "},").AppendLine();
        }

        Indent(builder, 1).AppendLine("]");
    }

    private static void WriteProp(StringBuilder builder, int depth, string name, string value, bool comma)
    {
        Indent(builder, depth).Append('"').Append(EscapeJson(name)).Append("\": \"").Append(EscapeJson(value)).Append('"');
        if (comma)
            builder.Append(',');
        builder.AppendLine();
    }

    private static void WriteProp(StringBuilder builder, int depth, string name, int value, bool comma)
    {
        Indent(builder, depth).Append('"').Append(EscapeJson(name)).Append("\": ").Append(value);
        if (comma)
            builder.Append(',');
        builder.AppendLine();
    }

    private static StringBuilder Indent(StringBuilder builder, int depth)
    {
        return builder.Append(' ', depth * 2);
    }

    private static string EscapeJson(string value)
    {
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
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }

        return builder.ToString();
    }

    private static string? GetArg(string[] args, string key)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], key, StringComparison.Ordinal) && i + 1 < args.Length)
                return args[i + 1];
        }

        return null;
    }

    private static string ToProjectPath(string repoRoot, string file)
    {
        string root = Path.GetFullPath(repoRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string full = Path.GetFullPath(file);
        if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            int start = root.Length;
            if (full.Length > start && (full[start] == Path.DirectorySeparatorChar || full[start] == Path.AltDirectorySeparatorChar))
                start++;

            return full[start..].Replace('\\', '/');
        }

        return full.Replace('\\', '/');
    }

    private sealed record Finding(string Path, int Line, string Owner, string Kind, string Detail, string Snippet);

    private sealed record ParseFailure(string Path, int Line, string Code);

    private sealed class Summary
    {
        public int ScannedFiles;
        public int ParseFailures;
        public int TotalFindings;
        public int ObjectCreations;
        public int ValueTypeCreations;
        public int ManagedRiskCreations;
        public int NativeTempJobAllocations;
        public int NativePersistentAllocations;
        public int NativeOtherAllocations;
        public int StringFormatCalls;
        public int ToStringCalls;
        public int LinqCalls;
        public int ForeachStatements;
        public int InterpolatedStrings;
        public int StringConcatSuspects;
    }
}
