using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace VaultNativeAliasRoslynAudit;

internal static class Program
{
    private const string Schema = "hecton8.vault_native_alias_roslyn_ledger.v1";
    private static readonly string[] NativeCollectionNames =
    {
        "NativeArray",
        "NativeSlice",
        "NativeList",
        "NativeHashMap",
        "NativeParallelHashMap",
        "NativeQueue",
        "UnsafeList"
    };

    private static readonly string[] JobInterfaceTokens =
    {
        "IJob",
        "IJobFor",
        "IJobParallelFor",
        "IJobParallelForTransform",
        "IJobChunk",
        "IJobEntity"
    };

    private static int Main(string[] args)
    {
        string repoRoot = ResolveRepoRoot(args);
        string sourceRoot = GetArg(args, "--root") ?? Path.Combine(repoRoot, "Assets", "_Project", "Scripts");
        string outputPath = GetArg(args, "--output") ?? Path.Combine(repoRoot, "Docs", "Reports", "VAULT_NATIVE_ALIAS_LEDGER_X_000.json");

        if (!Directory.Exists(sourceRoot))
        {
            Console.Error.WriteLine("Source root not found: " + sourceRoot);
            return 2;
        }

        List<Finding> findings = new(capacity: 4096);
        List<ParseFailure> parseFailures = new(capacity: 64);
        string[] files = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < files.Length; i++)
            ScanFile(repoRoot, files[i], findings, parseFailures);

        AuditSummary summary = BuildSummary(files.Length, findings, parseFailures);
        string canonical = BuildCanonicalHashInput(findings, parseFailures, summary);
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        string json = BuildJson(repoRoot, sourceRoot, outputPath, summary, findings, parseFailures, hash);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        File.WriteAllText(outputPath, json, new UTF8Encoding(false));

        Console.WriteLine(
            "Vault native alias Roslyn audit: " +
            "files=" + summary.ScannedFiles +
            ", parseFailures=" + summary.ParseFailures +
            ", totalFields=" + summary.TotalNativeFieldDeclarations +
            ", forbiddenPersistentCandidates=" + summary.ForbiddenPersistentCandidates +
            ", jobTransientFields=" + summary.JobTransientFields +
            ", coreMemoryAllowedFields=" + summary.CoreMemoryAllowedFields +
            ", hash=" + hash);

        return parseFailures.Count == 0 ? 0 : 1;
    }

    private static void ScanFile(
        string repoRoot,
        string file,
        List<Finding> findings,
        List<ParseFailure> parseFailures)
    {
        string relativePath = ToProjectPath(repoRoot, file);
        string source;
        try
        {
            source = File.ReadAllText(file, Encoding.UTF8);
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            parseFailures.Add(new ParseFailure(relativePath, 0, exception.GetType().Name));
            return;
        }

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

        foreach (FieldDeclarationSyntax field in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            TypeDeclarationSyntax? owner = field.FirstAncestorOrSelf<TypeDeclarationSyntax>();
            if (owner == null)
                continue;

            string typeText = field.Declaration.Type.ToString();
            bool isPointer = field.Declaration.Type is PointerTypeSyntax || typeText.Contains("*", StringComparison.Ordinal);
            string collectionName = string.Empty;
            if (!isPointer && !TryFindNativeCollectionName(typeText, out collectionName))
                continue;

            if (isPointer)
                collectionName = "Pointer";

            string ownerName = owner.Identifier.ValueText;
            string ownerKind = owner.Kind().ToString();
            string namespaceName = ResolveNamespace(owner);
            string bases = owner.BaseList?.ToString() ?? string.Empty;
            bool isJobField = IsJobOwner(owner, bases);
            bool isMonoBehaviour = bases.Contains("MonoBehaviour", StringComparison.Ordinal);
            bool isCoreMemoryAllowed = IsCoreMemoryAuthority(relativePath, namespaceName, ownerName);
            string classification = Classify(isCoreMemoryAllowed, isJobField);
            string attributes = string.Join(" ", field.AttributeLists.Select(static list => list.ToString()));
            string modifiers = field.Modifiers.ToString();
            int line = field.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

            foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
            {
                findings.Add(new Finding(
                    relativePath,
                    line,
                    namespaceName,
                    ownerName,
                    ownerKind,
                    bases,
                    isMonoBehaviour,
                    isJobField,
                    isCoreMemoryAllowed,
                    collectionName,
                    typeText,
                    variable.Identifier.ValueText,
                    modifiers,
                    attributes,
                    classification));
            }
        }
    }

    private static bool TryFindNativeCollectionName(string typeText, out string collectionName)
    {
        for (int i = 0; i < NativeCollectionNames.Length; i++)
        {
            string name = NativeCollectionNames[i];
            if (typeText.Contains(name + "<", StringComparison.Ordinal) ||
                typeText.Contains(name + ".", StringComparison.Ordinal))
            {
                collectionName = name;
                return true;
            }
        }

        collectionName = string.Empty;
        return false;
    }

    private static bool IsJobOwner(TypeDeclarationSyntax owner, string bases)
    {
        if (owner is not StructDeclarationSyntax && owner is not RecordDeclarationSyntax)
            return false;

        for (int i = 0; i < JobInterfaceTokens.Length; i++)
        {
            if (bases.Contains(JobInterfaceTokens[i], StringComparison.Ordinal))
                return true;
        }

        return owner.Identifier.ValueText.EndsWith("Job", StringComparison.Ordinal);
    }

    private static bool IsCoreMemoryAuthority(string relativePath, string namespaceName, string ownerName)
    {
        string normalized = relativePath.Replace('\\', '/');
        if (normalized.Contains("/Core/Memory/", StringComparison.OrdinalIgnoreCase))
            return true;

        if (namespaceName.Contains(".Core.Memory", StringComparison.Ordinal) ||
            namespaceName.EndsWith("Core.Memory", StringComparison.Ordinal))
            return true;

        return string.Equals(ownerName, "GlobalDataVault", StringComparison.Ordinal) ||
               string.Equals(ownerName, "H8Memory", StringComparison.Ordinal) ||
               string.Equals(ownerName, "NativeMemorySentinel", StringComparison.Ordinal);
    }

    private static string Classify(bool isCoreMemoryAllowed, bool isJobField)
    {
        if (isCoreMemoryAllowed)
            return "allowed_core_memory_authority";

        if (isJobField)
            return "allowed_transient_job_parameter";

        return "forbidden_persistent_native_alias_candidate";
    }

    private static string ResolveNamespace(SyntaxNode node)
    {
        BaseNamespaceDeclarationSyntax? baseNamespace = node.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>();
        if (baseNamespace != null)
            return baseNamespace.Name.ToString();

        NamespaceDeclarationSyntax? oldNamespace = node.FirstAncestorOrSelf<NamespaceDeclarationSyntax>();
        return oldNamespace != null ? oldNamespace.Name.ToString() : string.Empty;
    }

    private static AuditSummary BuildSummary(int scannedFiles, List<Finding> findings, List<ParseFailure> parseFailures)
    {
        AuditSummary summary = new()
        {
            ScannedFiles = scannedFiles,
            ParseFailures = parseFailures.Count,
            TotalNativeFieldDeclarations = findings.Count
        };

        for (int i = 0; i < findings.Count; i++)
        {
            Finding finding = findings[i];
            if (finding.Classification == "forbidden_persistent_native_alias_candidate")
                summary.ForbiddenPersistentCandidates++;
            else if (finding.Classification == "allowed_transient_job_parameter")
                summary.JobTransientFields++;
            else if (finding.Classification == "allowed_core_memory_authority")
                summary.CoreMemoryAllowedFields++;

            if (finding.CollectionName == "Pointer")
                summary.RawPointerFields++;

            if (finding.IsMonoBehaviour && finding.Classification == "forbidden_persistent_native_alias_candidate")
                summary.ForbiddenMonoBehaviourCandidates++;
        }

        return summary;
    }

    private static string BuildCanonicalHashInput(
        List<Finding> findings,
        List<ParseFailure> parseFailures,
        AuditSummary summary)
    {
        StringBuilder builder = new();
        builder.Append(Schema).Append('|')
            .Append(summary.ScannedFiles).Append('|')
            .Append(summary.TotalNativeFieldDeclarations).Append('|')
            .Append(summary.ForbiddenPersistentCandidates).Append('|')
            .Append(summary.ParseFailures).Append('\n');

        for (int i = 0; i < findings.Count; i++)
        {
            Finding finding = findings[i];
            builder.Append(finding.Path).Append(':')
                .Append(finding.Line).Append(':')
                .Append(finding.OwnerType).Append(':')
                .Append(finding.VariableName).Append(':')
                .Append(finding.CollectionName).Append(':')
                .Append(finding.Classification).Append('\n');
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
        string sourceRoot,
        string outputPath,
        AuditSummary summary,
        List<Finding> findings,
        List<ParseFailure> parseFailures,
        string auditHash)
    {
        StringBuilder builder = new(capacity: Math.Max(4096, findings.Count * 512));
        builder.AppendLine("{");
        WriteProp(builder, 1, "schema", Schema, comma: true);
        WriteProp(builder, 1, "agentId", "X_000", comma: true);
        WriteProp(builder, 1, "repoRoot", repoRoot, comma: true);
        WriteProp(builder, 1, "sourceRoot", sourceRoot, comma: true);
        WriteProp(builder, 1, "outputPath", outputPath, comma: true);
        WriteProp(builder, 1, "auditHashSha256", auditHash, comma: true);
        Indent(builder, 1).AppendLine("\"summary\": {");
        WriteProp(builder, 2, "scannedFiles", summary.ScannedFiles, comma: true);
        WriteProp(builder, 2, "parseFailures", summary.ParseFailures, comma: true);
        WriteProp(builder, 2, "totalNativeFieldDeclarations", summary.TotalNativeFieldDeclarations, comma: true);
        WriteProp(builder, 2, "forbiddenPersistentCandidates", summary.ForbiddenPersistentCandidates, comma: true);
        WriteProp(builder, 2, "forbiddenMonoBehaviourCandidates", summary.ForbiddenMonoBehaviourCandidates, comma: true);
        WriteProp(builder, 2, "jobTransientFields", summary.JobTransientFields, comma: true);
        WriteProp(builder, 2, "coreMemoryAllowedFields", summary.CoreMemoryAllowedFields, comma: true);
        WriteProp(builder, 2, "rawPointerFields", summary.RawPointerFields, comma: false);
        Indent(builder, 1).AppendLine("},");
        Indent(builder, 1).AppendLine("\"findings\": [");
        for (int i = 0; i < findings.Count; i++)
        {
            Finding finding = findings[i];
            Indent(builder, 2).AppendLine("{");
            WriteProp(builder, 3, "path", finding.Path, comma: true);
            WriteProp(builder, 3, "line", finding.Line, comma: true);
            WriteProp(builder, 3, "namespace", finding.NamespaceName, comma: true);
            WriteProp(builder, 3, "ownerType", finding.OwnerType, comma: true);
            WriteProp(builder, 3, "ownerKind", finding.OwnerKind, comma: true);
            WriteProp(builder, 3, "bases", finding.Bases, comma: true);
            WriteProp(builder, 3, "isMonoBehaviour", finding.IsMonoBehaviour, comma: true);
            WriteProp(builder, 3, "isJobField", finding.IsJobField, comma: true);
            WriteProp(builder, 3, "isCoreMemoryAllowed", finding.IsCoreMemoryAllowed, comma: true);
            WriteProp(builder, 3, "collection", finding.CollectionName, comma: true);
            WriteProp(builder, 3, "type", finding.TypeText, comma: true);
            WriteProp(builder, 3, "name", finding.VariableName, comma: true);
            WriteProp(builder, 3, "modifiers", finding.Modifiers, comma: true);
            WriteProp(builder, 3, "attributes", finding.Attributes, comma: true);
            WriteProp(builder, 3, "classification", finding.Classification, comma: false);
            Indent(builder, 2).Append(i == findings.Count - 1 ? "}" : "},").AppendLine();
        }

        Indent(builder, 1).AppendLine("],");
        Indent(builder, 1).AppendLine("\"parseFailures\": [");
        for (int i = 0; i < parseFailures.Count; i++)
        {
            ParseFailure failure = parseFailures[i];
            Indent(builder, 2).AppendLine("{");
            WriteProp(builder, 3, "path", failure.Path, comma: true);
            WriteProp(builder, 3, "line", failure.Line, comma: true);
            WriteProp(builder, 3, "code", failure.Code, comma: false);
            Indent(builder, 2).Append(i == parseFailures.Count - 1 ? "}" : "},").AppendLine();
        }

        Indent(builder, 1).AppendLine("]");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void WriteProp(StringBuilder builder, int depth, string name, string value, bool comma)
    {
        Indent(builder, depth)
            .Append('"').Append(EscapeJson(name)).Append("\": \"")
            .Append(EscapeJson(value)).Append('"');
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

    private static void WriteProp(StringBuilder builder, int depth, string name, bool value, bool comma)
    {
        Indent(builder, depth).Append('"').Append(EscapeJson(name)).Append("\": ").Append(value ? "true" : "false");
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

    private static string ResolveRepoRoot(string[] args)
    {
        string? explicitRoot = GetArg(args, "--repo");
        if (!string.IsNullOrWhiteSpace(explicitRoot))
            return Path.GetFullPath(explicitRoot);

        string current = Directory.GetCurrentDirectory();
        DirectoryInfo? cursor = new(current);
        while (cursor != null)
        {
            if (Directory.Exists(Path.Combine(cursor.FullName, "Assets")) &&
                Directory.Exists(Path.Combine(cursor.FullName, "Docs")))
            {
                return cursor.FullName;
            }

            cursor = cursor.Parent;
        }

        return current;
    }

    private static string? GetArg(string[] args, string key)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], key, StringComparison.Ordinal))
                continue;

            return i + 1 < args.Length ? args[i + 1] : null;
        }

        return null;
    }

    private static string ToProjectPath(string repoRoot, string file)
    {
        string fullRoot = Path.GetFullPath(repoRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string fullPath = Path.GetFullPath(file);
        if (fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            int start = fullRoot.Length;
            if (fullPath.Length > start && (fullPath[start] == Path.DirectorySeparatorChar || fullPath[start] == Path.AltDirectorySeparatorChar))
                start++;

            return fullPath[start..].Replace('\\', '/');
        }

        return fullPath.Replace('\\', '/');
    }

    private sealed record Finding(
        string Path,
        int Line,
        string NamespaceName,
        string OwnerType,
        string OwnerKind,
        string Bases,
        bool IsMonoBehaviour,
        bool IsJobField,
        bool IsCoreMemoryAllowed,
        string CollectionName,
        string TypeText,
        string VariableName,
        string Modifiers,
        string Attributes,
        string Classification);

    private sealed record ParseFailure(string Path, int Line, string Code);

    private sealed class AuditSummary
    {
        public int ScannedFiles;
        public int ParseFailures;
        public int TotalNativeFieldDeclarations;
        public int ForbiddenPersistentCandidates;
        public int ForbiddenMonoBehaviourCandidates;
        public int JobTransientFields;
        public int CoreMemoryAllowedFields;
        public int RawPointerFields;
    }
}
