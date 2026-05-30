using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SignalBusContractAuditCli;

internal static class Program
{
    private const string Agent = "SHINOBU_02";
    private static readonly Regex StructDeclarationRegex = new(@"^\s*(?:(?:public|internal|private|protected)\s+)*(?:(?:readonly|partial|unsafe|ref)\s+)*struct\s+([A-Za-z_][A-Za-z0-9_]*)\b", RegexOptions.Compiled);
    private static readonly Regex LayoutPack1Regex = new(@"\[StructLayout\([^\]]*Pack\s*=\s*1(?!\d)", RegexOptions.Compiled);
    private static readonly Regex ManagedEventRegex = new(@"\b(event\s+(System\.)?Action|UnityEvent|SendMessage\s*\(|BroadcastMessage\s*\(|SendMessageUpwards\s*\(|System\.Action|System\.Func|Action<|Func<)", RegexOptions.Compiled);
    private static readonly Regex StringFieldRegex = new(@"\b(string|System\.String)\s+[A-Za-z_][A-Za-z0-9_]*", RegexOptions.Compiled);
    private static readonly Regex TelemetryArrayRegex = new(@"\b(?<access>private|internal|public|protected)\s+(?:static\s+)?(?:readonly\s+)?(?:ref\s+)?NativeArray\s*<[^>]*(Telemetry|BlackBox|Signal)[^>]*>\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\b(?!\s*=>)\s*(?:=[^;]*)?;", RegexOptions.Compiled);
    private static readonly Regex SignalQueueRegex = new(@"\b(?:private|internal|public|protected)\s+(?:static\s+)?(?:readonly\s+)?NativeQueue\s*<[^>]*(Signal|Command|Packet)[^>]*>\s+([A-Za-z_][A-Za-z0-9_]*)\b", RegexOptions.Compiled);
    private static readonly Regex SyncIoRegex = new(@"\b(File|Directory)\.(Read|Write|Append|Open|Create|Delete)|new\s+FileStream\s*\(", RegexOptions.Compiled);
    private static readonly Regex Compute1024Regex = new(@"numthreads\s*\(\s*1024\s*,", RegexOptions.Compiled);
    private static readonly Regex ContainerTypeRegex = new(@"\b(?<kind>SignalBus|NativeQueue|NativeList|NativeArray)\s*<\s*(?<type>[A-Za-z_][A-Za-z0-9_]*)\s*>", RegexOptions.Compiled);
    private static readonly Regex CacheLineCriticalConfigureRegex = new(@"(?:global::)?(?:(?:[A-Za-z_][A-Za-z0-9_]*\.)*)SignalBus\s*<\s*(?:global::)?(?:(?:[A-Za-z_][A-Za-z0-9_]*\.)*)(?<type>[A-Za-z_][A-Za-z0-9_]*)\s*>\s*\.\s*ConfigureCacheLineCritical\b", RegexOptions.Compiled);
    private static readonly Regex FieldDeclarationTypeRegex = new(@"^\s*(?:\[[^\]]+\]\s*)*(?:public|internal|private|protected)\s+(?:readonly\s+)?(?<type>(?:[A-Za-z_][A-Za-z0-9_]*\.)*[A-Za-z_][A-Za-z0-9_]*)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?:[=;])", RegexOptions.Compiled);
    private static readonly Regex ConstructorDeclarationRegex = new(@"^\s*(?:(?:public|internal|private|protected)\s+)*(?:(?:static|unsafe|extern)\s+)*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\([^;]*\)\s*(?:where\b[^{]+)?\{?", RegexOptions.Compiled);
    private static readonly Regex MethodDeclarationRegex = new(@"^\s*(?:(?:public|internal|private|protected)\s+)*(?:(?:static|unsafe|virtual|override|sealed|async|readonly|extern)\s+)*(?:[A-Za-z_][A-Za-z0-9_<>,\[\]\.?\s]*\s+)+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\([^;]*\)\s*(?:where\b[^{]+)?\{?", RegexOptions.Compiled);
    private static readonly Regex ConstructorDeclarationStartRegex = new(@"^\s*(?:(?:public|internal|private|protected|static|unsafe|extern)\s+)+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\([^;{}]*$", RegexOptions.Compiled);
    private static readonly Regex MethodDeclarationStartRegex = new(@"^\s*(?:(?:public|internal|private|protected)\s+)*(?:(?:static|unsafe|virtual|override|sealed|async|readonly|extern)\s+)*(?:[A-Za-z_][A-Za-z0-9_<>,\[\]\.?\s]*\s+)+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\([^;{}]*$", RegexOptions.Compiled);
    private static readonly Regex StructLayoutAttributeRegex = new(@"\[\s*(?:[A-Za-z_][A-Za-z0-9_]*\.)*StructLayout\b", RegexOptions.Compiled);
    private static readonly Regex HotPathEnumerationRegex = new(@"\bforeach\s*\(|\.Where\s*\(|\.Select\s*\(|\.OrderBy\s*\(|\.ToList\s*\(|\.ToArray\s*\(|Enumerable\.", RegexOptions.Compiled);
    private static readonly Regex HotPathAllocationRegex = new(@"\bnew\s+(?:List\s*<|Dictionary\s*<|HashSet\s*<|Queue\s*<|Stack\s*<|StringBuilder\b|string\b|Regex\b|FileStream\b|MemoryStream\b|StringWriter\b|Action\b|Func\b|WaitForSeconds\b|GameObject\b|Material\b|Texture2D\b|RenderTexture\b|Mesh\b|[A-Za-z_][A-Za-z0-9_<>,\.\s]*\s*\[)", RegexOptions.Compiled);
    private static readonly Regex UnityLookupRegex = new(@"(?:Try)?GetComponent\s*(?:<|\()|FindObjectOfType|FindObjectsOfType|GameObject\.Find|Object\.Find", RegexOptions.Compiled);
    private static readonly Regex GlobalRegistryHotLookupRegex = new(@"\bGlobalRegistry\s*\.\s*(?:Get|TryGet|Resolve)\s*(?:<|\()", RegexOptions.Compiled);
    private static readonly Regex MaterialMutationRegex = new(@"\.material\b|Material\.Set(Float|Int|Color|Vector|Texture)|\.Set(Float|Int|Color|Vector|Texture)\s*\(", RegexOptions.Compiled);
    private static readonly Regex JobCompleteRegex = new(@"\.\s*Complete\s*\(|\bCompleteAll\s*\(", RegexOptions.Compiled);
    private static readonly Regex SetDataRegex = new(@"\.\s*SetData\s*(?:<[^>]+>)?\s*\(", RegexOptions.Compiled);
    private static readonly Regex GetDataRegex = new(@"\.\s*GetData\s*(?:<[^>]+>)?\s*\(", RegexOptions.Compiled);
    private static readonly Regex GlobalSignalsDirectUseRegex = new(@"\bGlobalSignals\s*\.\s*(?<member>[A-Za-z_][A-Za-z0-9_]*)\b", RegexOptions.Compiled);

    private static int Main(string[] args)
    {
        try
        {
            var options = CliOptions.Parse(args);
            var scanner = new AuditScanner(options);
            var result = scanner.Run();

            if (!options.NoOutput)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(options.OutputJson)!);
                Directory.CreateDirectory(Path.GetDirectoryName(options.OutputMarkdown)!);

                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };
                File.WriteAllText(options.OutputJson, JsonSerializer.Serialize(result, jsonOptions), new UTF8Encoding(false));
                File.WriteAllText(options.OutputMarkdown, MarkdownWriter.Write(result), new UTF8Encoding(false));
            }

            Console.WriteLine("SignalBusContractAuditCli: files={0} shaders={1} errors={2} warnings={3} infos={4} confirmedErrors={5} output={6}",
                result.ScannedFiles,
                result.ShaderFilesScanned,
                result.Errors,
                result.Warnings,
                result.Infos,
                result.ConfirmedErrors,
                options.NoOutput ? "disabled" : options.OutputJson + " | " + options.OutputMarkdown);

            if (options.PrintFindings)
                PrintFindingsToConsole(result, options.MaxConsoleFindings);

            return options.FailOnError && result.Errors > 0 ? 2 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("SignalBusContractAuditCli failed: " + ex.Message);
            return 1;
        }
    }

    private static void PrintFindingsToConsole(AuditResult result, int maxFindings)
    {
        int printed = 0;
        foreach (var finding in result.Findings
            .OrderBy(item => item.Severity == "ERROR" ? 0 : item.Severity == "WARN" ? 1 : 2)
            .ThenByDescending(item => item.Confidence)
            .ThenBy(item => item.Path, StringComparer.Ordinal)
            .ThenBy(item => item.Line))
        {
            if (printed >= maxFindings)
                break;

            Console.WriteLine(
                "{0} {1}% {2} {3}:{4} {5}",
                finding.Severity,
                finding.Confidence,
                finding.Rule,
                finding.Path,
                finding.Line,
                finding.Symbol);
            Console.WriteLine("  " + finding.Evidence);
            printed++;
        }

        if (result.Findings.Length > printed)
            Console.WriteLine("... " + (result.Findings.Length - printed) + " findings omitted; raise --max-findings for console triage.");
    }

    private sealed class AuditScanner
    {
        private readonly CliOptions _options;
        private readonly List<Finding> _findings = [];
        private readonly List<SignalDefinition> _signalDefinitions = [];
        private readonly Dictionary<string, List<Pack1StructInfo>> _pack1StructsByName = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<StructLayoutInfo>> _structLayoutsByName = new(StringComparer.Ordinal);
        private int _scannedFiles;
        private int _shaderFilesScanned;
        private int _pack1Count;
        private int _runtimeSignalPack1Count;
        private int _transitivePack1FieldCount;
        private int _managedEventCount;
        private int _localNativeTelemetryCount;
        private int _registeredLocalTelemetryCount;
        private int _localNativeQueueCount;
        private int _computeThreadGroupRiskCount;
        private int _hotPathRiskCount;
        private int _coldSyncIoCount;
        private int _asmdefContractBoundaryCount;
        private int _cacheLineCriticalStrideDebtCount;

        public AuditScanner(CliOptions options)
        {
            _options = options;
        }

        public AuditResult Run()
        {
            var scriptsRoot = Path.Combine(_options.ProjectRoot, "Assets", "_Project", "Scripts");
            var assetsRoot = Path.Combine(_options.ProjectRoot, "Assets");
            if (!Directory.Exists(scriptsRoot))
            {
                throw new DirectoryNotFoundException("Scripts root not found: " + scriptsRoot);
            }

            var allScriptFiles = Directory.EnumerateFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories).ToArray();
            BuildPack1StructIndex(allScriptFiles);
            BuildStructLayoutIndex(allScriptFiles);

            foreach (var file in EnumerateScriptFiles(scriptsRoot))
            {
                ScanCSharpFile(file);
            }

            if (Directory.Exists(assetsRoot))
            {
                foreach (var file in Directory.EnumerateFiles(assetsRoot, "*.compute", SearchOption.AllDirectories))
                {
                    ScanComputeFile(file);
                }
            }

            ScanAssemblyContractBoundaries(scriptsRoot);
            AddDuplicateFindings();
            return BuildResult();
        }

        private IEnumerable<string> EnumerateScriptFiles(string scriptsRoot)
        {
            bool signalCriticalOnly = string.Equals(_options.Scope, "SignalCritical", StringComparison.OrdinalIgnoreCase);
            foreach (var path in Directory.EnumerateFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (signalCriticalOnly)
                {
                    var relative = ToRelativePath(path);
                    if (!Regex.IsMatch(relative, @"Assets/_Project/Scripts/Core/GlobalSignals\.cs$|Assets/_Project/Scripts/Core/Signals/|Assets/_Project/Scripts/Core/SystemDispatcher\.cs$|Assets/_Project/Scripts/Editor/SignalTrafficMonitorWindow\.cs$"))
                    {
                        continue;
                    }
                }

                yield return path;
            }
        }

        private void ScanAssemblyContractBoundaries(string scriptsRoot)
        {
            var asmdefs = new List<AsmdefInfo>();
            foreach (var asmdefFile in Directory.EnumerateFiles(scriptsRoot, "*.asmdef", SearchOption.AllDirectories))
            {
                var asmdef = TryReadAsmdef(asmdefFile);
                if (asmdef is not null)
                {
                    asmdefs.Add(asmdef);
                }
            }

            if (asmdefs.Count == 0)
            {
                return;
            }

            foreach (var file in EnumerateScriptFiles(scriptsRoot))
            {
                var text = File.ReadAllText(file);
                if (!UsesSignalContracts(text))
                {
                    continue;
                }

                var owner = FindNearestAsmdef(file, asmdefs);
                if (owner is null ||
                    string.Equals(owner.Name, "Hecton8.Core.Contracts", StringComparison.Ordinal) ||
                    owner.References.Contains("Hecton8.Core.Contracts"))
                {
                    continue;
                }

                _asmdefContractBoundaryCount++;
                AddFinding(
                    "WARN",
                    "ASMDEF_SIGNAL_CONTRACT_REFERENCE_MISSING",
                    85,
                    "COMPILE_WALL_DEPENDENCY_REVIEW",
                    "ASMDEF_REFERENCE_SCAN",
                    owner.RelativePath,
                    owner.ReferenceLine,
                    owner.Name,
                    "Signal contract source: " + ToRelativePath(file),
                    "Add a direct Hecton8.Core.Contracts asmdef reference for signal contract usage. Keep Hecton8.Core only when this assembly also consumes runtime Core APIs.",
                    new Dictionary<string, object?>
                    {
                        ["source"] = ToRelativePath(file),
                        ["hasCoreReference"] = owner.References.Contains("Hecton8.Core"),
                        ["hasContractsReference"] = false
                    });
            }
        }

        private void ScanCSharpFile(string path)
        {
            var relativePath = ToRelativePath(path);
            var rawText = File.ReadAllText(path);
            _scannedFiles++;

            if (!HasRelevantText(rawText))
            {
                return;
            }

            var rawLines = File.ReadAllLines(path);
            var codeLines = rawLines.Select(RemoveCodeTrivia).ToArray();
            var isEditor = IsEditorPath(relativePath);
            var isCoreSignalFile = IsCoreSignalFile(relativePath);
            var containerTypes = GetContainerTypes(codeLines);
            var structs = CollectStructs(relativePath, rawLines, codeLines, containerTypes);
            ScanCacheLineCriticalLanes(relativePath, rawLines, codeLines, structs);
            var structByIndex = structs.ToDictionary(item => item.Index);

            StructMetadata? currentStruct = null;
            var currentStructIsSignalCandidate = false;
            var currentStructIsStrictRuntimeContract = false;
            var structBraceDepth = 0;
            var structStarted = false;
            var currentMethodName = "";
            var methodBraceDepth = 0;
            var methodStarted = false;
            var preprocessorFrames = new List<PreprocessorFrame>(4);
            var pendingMethodName = "";
            var pendingMethodIsConstructor = false;

            for (var lineIndex = 0; lineIndex < codeLines.Length; lineIndex++)
            {
                var rawLine = rawLines[lineIndex];
                var code = codeLines[lineIndex];
                var lineNumber = lineIndex + 1;
                if (TryUpdatePreprocessorFrame(code, preprocessorFrames))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                if (pendingMethodName.Length > 0)
                {
                    if (code.Contains("=>", StringComparison.Ordinal) || code.Contains(';', StringComparison.Ordinal))
                    {
                        pendingMethodName = "";
                        pendingMethodIsConstructor = false;
                        continue;
                    }

                    if (!code.Contains('{', StringComparison.Ordinal))
                    {
                        continue;
                    }

                    currentMethodName = pendingMethodIsConstructor ? ".ctor" : pendingMethodName;
                    methodBraceDepth = CountBraceDelta(code);
                    methodStarted = true;
                    pendingMethodName = "";
                    pendingMethodIsConstructor = false;
                    if (methodBraceDepth <= 0 && code.Contains('}', StringComparison.Ordinal))
                    {
                        currentMethodName = "";
                        methodStarted = false;
                        methodBraceDepth = 0;
                    }

                    continue;
                }

                var structMatch = StructDeclarationRegex.Match(code);
                if (structMatch.Success)
                {
                    currentStruct = structByIndex.GetValueOrDefault(lineIndex);
                    currentStructIsSignalCandidate = currentStruct is not null &&
                        (currentStruct.ImplementsISignal ||
                         currentStruct.IsSignalLikeName ||
                         containerTypes.SignalBus.Contains(currentStruct.Name) ||
                         containerTypes.NativeQueue.Contains(currentStruct.Name));
                    currentStructIsStrictRuntimeContract = currentStruct is not null &&
                        !currentStruct.IsEditor &&
                        (currentStruct.ImplementsISignal ||
                         currentStruct.IsCoreSignalFile ||
                         containerTypes.SignalBus.Contains(currentStruct.Name) ||
                         containerTypes.NativeQueue.Contains(currentStruct.Name));
                    structBraceDepth = CountBraceDelta(code);
                    structStarted = code.Contains('{');
                }
                else if (currentStruct is not null)
                {
                    if (code.Contains('{'))
                    {
                        structStarted = true;
                    }

                    if (structStarted)
                    {
                        structBraceDepth += CountBraceDelta(code);
                        if (structBraceDepth <= 0 && code.Contains('}'))
                        {
                            currentStruct = null;
                            currentStructIsSignalCandidate = false;
                            currentStructIsStrictRuntimeContract = false;
                            structStarted = false;
                            structBraceDepth = 0;
                        }
                    }
                }

                var constructorMatch = ConstructorDeclarationRegex.Match(code);
                var methodMatch = constructorMatch.Success ? Match.Empty : MethodDeclarationRegex.Match(code);
                if (constructorMatch.Success &&
                    !code.Contains(';', StringComparison.Ordinal) &&
                    !code.Contains("=>", StringComparison.Ordinal) &&
                    !Regex.IsMatch(code, @"\b(class|struct|interface|enum|if|for|foreach|while|switch|catch|using|lock)\b"))
                {
                    if (!code.Contains('{', StringComparison.Ordinal))
                    {
                        currentMethodName = "";
                        methodStarted = false;
                        methodBraceDepth = 0;
                        pendingMethodName = constructorMatch.Groups["name"].Value;
                        pendingMethodIsConstructor = true;
                        continue;
                    }

                    currentMethodName = ".ctor";
                    methodBraceDepth = CountBraceDelta(code);
                    methodStarted = true;
                    if (methodBraceDepth <= 0 && code.Contains('}', StringComparison.Ordinal))
                    {
                        currentMethodName = "";
                        methodStarted = false;
                        methodBraceDepth = 0;
                    }
                }
                else if (methodMatch.Success &&
                    !code.Contains(';', StringComparison.Ordinal) &&
                    !code.Contains("=>", StringComparison.Ordinal) &&
                    !Regex.IsMatch(code, @"\b(class|struct|interface|enum)\b"))
                {
                    if (!code.Contains('{', StringComparison.Ordinal))
                    {
                        currentMethodName = "";
                        methodStarted = false;
                        methodBraceDepth = 0;
                        pendingMethodName = methodMatch.Groups["name"].Value;
                        pendingMethodIsConstructor = false;
                        continue;
                    }

                    currentMethodName = methodMatch.Groups["name"].Value;
                    methodBraceDepth = CountBraceDelta(code);
                    methodStarted = true;
                    if (methodBraceDepth <= 0 && code.Contains('}', StringComparison.Ordinal))
                    {
                        currentMethodName = "";
                        methodStarted = false;
                        methodBraceDepth = 0;
                    }
                }
                else if (!methodStarted && TryStartPendingMethodDeclaration(code, out pendingMethodName, out pendingMethodIsConstructor))
                {
                    continue;
                }
                else if (methodStarted && currentMethodName.Length > 0)
                {
                    methodBraceDepth += CountBraceDelta(code);
                    if (methodBraceDepth <= 0 && code.Contains('}', StringComparison.Ordinal))
                    {
                        currentMethodName = "";
                        methodStarted = false;
                        methodBraceDepth = 0;
                    }
                }

                var effectiveIsEditor = isEditor || IsInsideEditorOnlyPreprocessor(preprocessorFrames);
                ScanPack1(relativePath, rawLine, code, codeLines, lineNumber, lineIndex, effectiveIsEditor, isCoreSignalFile, containerTypes, structs);
                ScanManagedEventSurface(relativePath, rawLine, code, lineNumber, effectiveIsEditor);
                ScanManagedStringPayload(relativePath, rawLine, code, lineNumber, effectiveIsEditor, currentStruct, currentStructIsSignalCandidate, currentStructIsStrictRuntimeContract);
                ScanTransitivePack1Field(relativePath, rawLine, code, lineNumber, currentStruct, currentStructIsStrictRuntimeContract);
                ScanTelemetryRing(relativePath, rawText, rawLine, code, lineNumber, effectiveIsEditor, currentStruct);
                ScanLocalSignalQueue(relativePath, rawText, rawLine, code, lineNumber, effectiveIsEditor);
                ScanGlobalSignalsDirectUse(relativePath, rawLine, code, lineNumber, effectiveIsEditor, currentMethodName);
                ScanSyncRuntimeIo(relativePath, rawLine, code, lineNumber, effectiveIsEditor, currentMethodName);
                if (_options.IncludeHotPathHeuristics)
                {
                    ScanHotPathHeuristics(relativePath, rawLine, code, lineNumber, effectiveIsEditor, currentMethodName);
                }
            }
        }

        private void BuildPack1StructIndex(IEnumerable<string> files)
        {
            foreach (var path in files)
            {
                var relativePath = ToRelativePath(path);
                var rawText = File.ReadAllText(path);
                if (!ContainsAny(rawText, "StructLayout", "Pack"))
                {
                    continue;
                }

                var rawLines = File.ReadAllLines(path);
                var codeLines = rawLines.Select(RemoveCodeTrivia).ToArray();
                for (var lineIndex = 0; lineIndex < codeLines.Length; lineIndex++)
                {
                    var code = codeLines[lineIndex];
                    if (code.IndexOf("StructLayout", StringComparison.Ordinal) < 0 ||
                        code.IndexOf("Pack", StringComparison.Ordinal) < 0 ||
                        !LayoutPack1Regex.IsMatch(code))
                    {
                        continue;
                    }

                    var structIndex = FindStructDeclarationNearAttribute(codeLines, lineIndex);
                    if (structIndex < 0)
                    {
                        continue;
                    }

                    var match = StructDeclarationRegex.Match(codeLines[structIndex]);
                    if (!match.Success)
                    {
                        continue;
                    }

                    var name = match.Groups[1].Value;
                    var info = new Pack1StructInfo(
                        name,
                        relativePath,
                        structIndex + 1,
                        IsEditorPath(relativePath),
                        IsFileFormatLike(relativePath, name),
                        StructBodyContainsWideField(codeLines, structIndex));

                    if (!_pack1StructsByName.TryGetValue(name, out var entries))
                    {
                        entries = [];
                        _pack1StructsByName.Add(name, entries);
                    }

                    if (!entries.Any(item => item.Path == info.Path && item.Line == info.Line))
                    {
                        entries.Add(info);
                    }
                }
            }
        }

        private void BuildStructLayoutIndex(IEnumerable<string> files)
        {
            foreach (var path in files)
            {
                var relativePath = ToRelativePath(path);
                var rawText = File.ReadAllText(path);
                if (!rawText.Contains("struct", StringComparison.Ordinal))
                {
                    continue;
                }

                var rawLines = File.ReadAllLines(path);
                var codeLines = rawLines.Select(RemoveCodeTrivia).ToArray();
                for (var lineIndex = 0; lineIndex < codeLines.Length; lineIndex++)
                {
                    var match = StructDeclarationRegex.Match(codeLines[lineIndex]);
                    if (!match.Success)
                    {
                        continue;
                    }

                    var name = match.Groups[1].Value;
                    var info = new StructLayoutInfo(
                        name,
                        relativePath,
                        lineIndex + 1,
                        HasStructLayoutBefore(codeLines, lineIndex),
                        StructLayoutSizeBefore(codeLines, lineIndex),
                        IsEditorPath(relativePath),
                        IsCoreSignalFile(relativePath));

                    if (!_structLayoutsByName.TryGetValue(name, out var entries))
                    {
                        entries = [];
                        _structLayoutsByName.Add(name, entries);
                    }

                    if (!entries.Any(item => item.Path == info.Path && item.Line == info.Line))
                    {
                        entries.Add(info);
                    }
                }
            }
        }

        private List<StructMetadata> CollectStructs(string relativePath, string[] rawLines, string[] codeLines, ContainerTypes containerTypes)
        {
            var structs = new List<StructMetadata>();
            for (var lineIndex = 0; lineIndex < codeLines.Length; lineIndex++)
            {
                var match = StructDeclarationRegex.Match(codeLines[lineIndex]);
                if (!match.Success)
                {
                    continue;
                }

                var name = match.Groups[1].Value;
                var metadata = new StructMetadata(
                    name,
                    codeLines[lineIndex].Trim(),
                    relativePath,
                    lineIndex + 1,
                    lineIndex,
                    HasStructLayoutBefore(codeLines, lineIndex),
                    StructLayoutSizeBefore(codeLines, lineIndex),
                    StructImplementsISignal(codeLines, lineIndex),
                    StructImplementsBurstJob(codeLines, lineIndex),
                    StructBodyContainsExecuteMethod(codeLines, lineIndex),
                    IsEditorPath(relativePath),
                    string.Equals(relativePath, "Assets/_Project/Scripts/Core/GlobalSignals.cs", StringComparison.Ordinal),
                    IsCoreSignalFile(relativePath),
                    IsSignalLikeName(name));

                structs.Add(metadata);
                if ((metadata.IsSignalLikeName || metadata.ImplementsISignal) &&
                    !metadata.ImplementsBurstJob &&
                    !metadata.HasExecuteMethod)
                {
                    var strictSignal = !metadata.IsEditor &&
                        (metadata.ImplementsISignal ||
                         metadata.IsCoreSignalFile ||
                         containerTypes.SignalBus.Contains(name) ||
                         containerTypes.NativeQueue.Contains(name));
                    _signalDefinitions.Add(new SignalDefinition(metadata.Name, metadata.Path, metadata.Line, metadata.HasStructLayout, metadata.ImplementsISignal, metadata.IsEditor, metadata.IsCoreGlobalSignals, strictSignal));
                }

                var strictSignalForLayout = !metadata.IsEditor &&
                    (metadata.ImplementsISignal || metadata.IsCoreSignalFile || containerTypes.SignalBus.Contains(name) || containerTypes.NativeQueue.Contains(name));
                var advisorySignal = metadata.IsSignalLikeName || Regex.IsMatch(name, "(Signal|Command|Packet)$");
                if (advisorySignal && !metadata.HasStructLayout)
                {
                    if (strictSignalForLayout)
                    {
                        AddFinding("WARN", "SIGNAL_LAYOUT_UNDECLARED", 86, "PROBABLE_RUNTIME_PAYLOAD", "ANCHORED_STRUCT_DECLARATION", relativePath, lineIndex + 1, name, rawLines[lineIndex], "Add explicit StructLayout or document unmanaged field order before this payload crosses Burst/native/binary boundaries.",
                            new Dictionary<string, object?> { ["isEditor"] = metadata.IsEditor, ["implementsISignal"] = metadata.ImplementsISignal, ["isCoreSignalFile"] = metadata.IsCoreSignalFile });
                    }
                    else if (metadata.IsEditor)
                    {
                        AddFinding("INFO", "EDITOR_SIGNAL_LAYOUT_REVIEW", 55, "EDITOR_ONLY_REVIEW", "ANCHORED_STRUCT_DECLARATION", relativePath, lineIndex + 1, name, rawLines[lineIndex], "Editor/test signal-like structs do not gate runtime, but should not shadow production contracts.",
                            new Dictionary<string, object?> { ["isEditor"] = metadata.IsEditor, ["implementsISignal"] = metadata.ImplementsISignal });
                    }
                    else if (metadata.ImplementsBurstJob)
                    {
                        AddFinding("INFO", "JOB_STRUCT_LAYOUT_REVIEW", 54, "BURST_JOB_STRUCT_REVIEW", "ANCHORED_STRUCT_DECLARATION", relativePath, lineIndex + 1, name, rawLines[lineIndex], "This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.",
                            new Dictionary<string, object?> { ["isEditor"] = metadata.IsEditor, ["implementsISignal"] = metadata.ImplementsISignal, ["implementsBurstJob"] = true });
                    }
                    else if (metadata.HasExecuteMethod)
                    {
                        AddFinding("INFO", "EXECUTABLE_STRUCT_LAYOUT_REVIEW", 54, "EXECUTABLE_CARRIER_STRUCT_REVIEW", "ANCHORED_STRUCT_DECLARATION", relativePath, lineIndex + 1, name, rawLines[lineIndex], "This signal-like name belongs to an executable carrier with Execute(), not a binary payload contract. Keep NativeArray handles owner-owned; add StructLayout only if the carrier itself is serialized or copied as raw bytes.",
                            new Dictionary<string, object?> { ["isEditor"] = metadata.IsEditor, ["implementsISignal"] = metadata.ImplementsISignal, ["hasExecuteMethod"] = true });
                    }
                    else
                    {
                        AddFinding("WARN", "SIGNAL_LAYOUT_REVIEW", 65, "NAME_BASED_REVIEW", "ANCHORED_STRUCT_DECLARATION", relativePath, lineIndex + 1, name, rawLines[lineIndex], "Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.",
                            new Dictionary<string, object?> { ["isEditor"] = metadata.IsEditor, ["implementsISignal"] = metadata.ImplementsISignal });
                    }
                }
            }

            return structs;
        }

        private void ScanCacheLineCriticalLanes(string relativePath, string[] rawLines, string[] codeLines, List<StructMetadata> structs)
        {
            var structByName = structs
                .GroupBy(item => item.Name, StringComparer.Ordinal)
                .ToDictionary(item => item.Key, item => item.First(), StringComparer.Ordinal);

            for (var lineIndex = 0; lineIndex < codeLines.Length; lineIndex++)
            {
                var code = codeLines[lineIndex];
                if (!code.Contains("SignalBus", StringComparison.Ordinal))
                {
                    continue;
                }

                var statement = BuildForwardStatement(codeLines, lineIndex);
                if (!statement.Text.Contains("ConfigureCacheLineCritical", StringComparison.Ordinal))
                {
                    continue;
                }

                var match = CacheLineCriticalConfigureRegex.Match(statement.Text);
                if (!match.Success)
                {
                    continue;
                }

                var laneType = match.Groups["type"].Value;
                var layoutSize = 0;
                var structLine = 0;
                var structPath = "";
                if (structByName.TryGetValue(laneType, out var metadata))
                {
                    layoutSize = metadata.LayoutSize;
                    structLine = metadata.Line;
                    structPath = metadata.Path;
                }

                if ((layoutSize <= 0 || structLine <= 0) &&
                    TryResolveGlobalStructLayout(laneType, out var globalLayout))
                {
                    layoutSize = globalLayout.LayoutSize;
                    structLine = globalLayout.Line;
                    structPath = globalLayout.Path;
                }

                if (layoutSize is 64 or 128)
                {
                    continue;
                }

                _cacheLineCriticalStrideDebtCount++;
                AddFinding(
                    "INFO",
                    "CACHELINE_CRITICAL_SIGNAL_STRIDE_DEBT",
                    88,
                    "CACHELINE_CRITICAL_TELEMETRY_DEBT",
                    "CONFIGURE_CACHELINE_CRITICAL_CALL",
                    relativePath,
                    lineIndex + 1,
                    laneType,
                    BuildRawStatementEvidence(rawLines, lineIndex, statement.EndIndex),
                    "This cache-line-critical lane currently has a payload stride outside 64/128 bytes. Keep telemetry flag bit 32 active and migrate to a 64/128-byte payload or split gameplay truth from visual sidecar before raising cadence.",
                    new Dictionary<string, object?>
                    {
                        ["payloadSize"] = layoutSize,
                        ["expectedStride"] = "64_OR_128",
                        ["structLine"] = structLine,
                        ["structPath"] = structPath,
                        ["statementLineSpan"] = statement.EndIndex - lineIndex + 1
                    });
            }
        }

        private bool TryResolveGlobalStructLayout(string name, out StructLayoutInfo layout)
        {
            layout = default!;
            if (!_structLayoutsByName.TryGetValue(name, out var entries) || entries.Count == 0)
            {
                return false;
            }

            var resolved = entries
                .Where(item => !item.IsEditor && item.HasStructLayout && item.LayoutSize > 0)
                .OrderByDescending(item => item.IsCoreSignalFile)
                .ThenBy(item => item.Path, StringComparer.Ordinal)
                .ThenBy(item => item.Line)
                .FirstOrDefault();
            if (resolved is not null)
            {
                layout = resolved;
                return true;
            }

            resolved = entries
                .Where(item => !item.IsEditor)
                .OrderByDescending(item => item.IsCoreSignalFile)
                .ThenBy(item => item.Path, StringComparer.Ordinal)
                .ThenBy(item => item.Line)
                .FirstOrDefault();
            if (resolved is null)
            {
                return false;
            }

            layout = resolved;
            return true;
        }

        private void ScanPack1(string relativePath, string rawLine, string code, string[] codeLines, int lineNumber, int lineIndex, bool isEditor, bool isCoreSignalFile, ContainerTypes containerTypes, List<StructMetadata> structs)
        {
            if (code.IndexOf("StructLayout", StringComparison.Ordinal) < 0 ||
                code.IndexOf("Pack", StringComparison.Ordinal) < 0 ||
                !LayoutPack1Regex.IsMatch(code))
            {
                return;
            }

            _pack1Count++;
            var metadata = FindNearestStructMetadata(structs, lineIndex);
            var symbol = metadata?.Name ?? "";
            var implementsISignal = metadata?.ImplementsISignal ?? false;
            var symbolIsEditor = metadata?.IsEditor ?? isEditor;
            var symbolIsCoreSignalFile = metadata?.IsCoreSignalFile ?? isCoreSignalFile;
            var symbolIsSignalLike = metadata?.IsSignalLikeName ?? false;
            var usedAsSignalBus = containerTypes.SignalBus.Contains(symbol);
            var usedAsNativeQueue = containerTypes.NativeQueue.Contains(symbol);
            var usedAsNativeList = containerTypes.NativeList.Contains(symbol);
            var nativeQueueSignalPayload = usedAsNativeQueue &&
                (symbolIsSignalLike || Regex.IsMatch(symbol, "(Signal|Command|Packet|Event|Payload)$", RegexOptions.CultureInvariant));
            var usedAsSignalContainer = usedAsSignalBus || usedAsNativeQueue;
            var usedAsNativeArray = containerTypes.NativeArray.Contains(symbol);
            var fileFormatLike = IsFileFormatLike(relativePath, symbol);
            var strictRuntimeSignal = !symbolIsEditor &&
                (implementsISignal || symbolIsCoreSignalFile || usedAsSignalBus || nativeQueueSignalPayload);
            var wideField = StructBodyContainsWideField(codeLines, lineIndex);

            if (strictRuntimeSignal)
            {
                _runtimeSignalPack1Count++;
                var confidence = implementsISignal || usedAsSignalContainer ? 96 : 90;
                AddFinding("ERROR", "RUNTIME_SIGNAL_PACK1_FORBIDDEN", confidence, "CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL", "STRUCTLAYOUT_ATTRIBUTE", relativePath, lineNumber, symbol, rawLine, "Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.",
                    new Dictionary<string, object?> { ["isEditor"] = symbolIsEditor, ["implementsISignal"] = implementsISignal, ["isCoreSignalFile"] = symbolIsCoreSignalFile, ["usedAsSignalContainer"] = usedAsSignalContainer, ["usedAsNativeList"] = usedAsNativeList, ["fileFormatLike"] = fileFormatLike });
            }
            else if (symbolIsEditor)
            {
                AddFinding("INFO", "EDITOR_PACK1_REVIEW", 50, "EDITOR_ONLY_REVIEW", "STRUCTLAYOUT_ATTRIBUTE", relativePath, lineNumber, symbol, rawLine, "Editor/test Pack=1 does not gate runtime memory, but avoid copying it into player DTOs.",
                    new Dictionary<string, object?> { ["isEditor"] = true, ["fileFormatLike"] = fileFormatLike });
            }
            else if (symbolIsSignalLike || usedAsNativeArray)
            {
                AddFinding("WARN", "PACK1_RUNTIME_NATIVE_REVIEW", 78, "PROBABLE_RUNTIME_NATIVE_PAYLOAD", "STRUCTLAYOUT_ATTRIBUTE", relativePath, lineNumber, symbol, rawLine, "Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.",
                    new Dictionary<string, object?> { ["isEditor"] = false, ["isSignalLikeName"] = symbolIsSignalLike, ["usedAsNativeArray"] = usedAsNativeArray });
            }
            else if (fileFormatLike)
            {
                AddFinding("INFO", "PACK1_FILE_FORMAT_BOUNDARY_REVIEW", 62, "FILE_FORMAT_OR_SERIALIZATION_CANDIDATE", "STRUCTLAYOUT_ATTRIBUTE", relativePath, lineNumber, symbol, rawLine, "If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.",
                    new Dictionary<string, object?> { ["isEditor"] = false, ["fileFormatLike"] = true, ["usedAsNativeArray"] = usedAsNativeArray });
            }
            else
            {
                AddFinding("WARN", "PACK1_REQUIRES_OWNER_JUSTIFICATION", 68, "STATIC_LAYOUT_REVIEW", "STRUCTLAYOUT_ATTRIBUTE", relativePath, lineNumber, symbol, rawLine, "Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.",
                    new Dictionary<string, object?> { ["isEditor"] = false, ["fileFormatLike"] = fileFormatLike });
            }

            if (!wideField)
            {
                return;
            }

            if (strictRuntimeSignal || (symbolIsSignalLike && !symbolIsEditor))
            {
                AddFinding("ERROR", "PACK1_WIDE_FIELD_ALIGNMENT_RISK", 98, "CONFIRMED_ARM64_ALIGNMENT_RISK", "STRUCT_BODY_FIELD_SCAN", relativePath, lineNumber, symbol, rawLine, "This Pack=1 struct contains double/long/pointer-sized fields. Reorder 8-byte fields first and add explicit padding to 8-byte size.",
                    new Dictionary<string, object?> { ["isEditor"] = symbolIsEditor, ["implementsISignal"] = implementsISignal, ["usedAsSignalContainer"] = usedAsSignalContainer, ["usedAsNativeList"] = usedAsNativeList });
            }
            else
            {
                AddFinding("WARN", "PACK1_WIDE_FIELD_REVIEW", 84, "PROBABLE_ARM64_ALIGNMENT_RISK", "STRUCT_BODY_FIELD_SCAN", relativePath, lineNumber, symbol, rawLine, "Pack=1 plus 8-byte fields is risky on ARM64 even outside signal lanes. Verify it never enters runtime native memory.",
                    new Dictionary<string, object?> { ["isEditor"] = symbolIsEditor, ["fileFormatLike"] = fileFormatLike });
            }
        }

        private void ScanManagedEventSurface(string relativePath, string rawLine, string code, int lineNumber, bool isEditor)
        {
            if (!ContainsAny(code, "Action", "Func", "UnityEvent", "SendMessage", "BroadcastMessage") ||
                !Regex.IsMatch(relativePath, @"Signal|Signals|Events|Core/GlobalSignals\.cs|Core/Contracts") ||
                !ManagedEventRegex.IsMatch(code))
            {
                return;
            }

            _managedEventCount++;
            if (isEditor)
            {
                AddFinding("WARN", "EDITOR_MANAGED_EVENT_SURFACE_REVIEW", 62, "EDITOR_ONLY_REVIEW", "SANITIZED_LINE_REGEX", relativePath, lineNumber, "", rawLine, "Editor managed delegates are not runtime transport, but do not copy this surface into player signal paths.",
                    new Dictionary<string, object?> { ["isEditor"] = true });
            }
            else
            {
                AddFinding("ERROR", "MANAGED_EVENT_SURFACE_IN_SIGNAL_DOMAIN", 88, "PROBABLE_RUNTIME_TRANSPORT_VIOLATION", "SANITIZED_LINE_REGEX", relativePath, lineNumber, "", rawLine, "Route broadcasts through unmanaged SignalBus<T> lanes or cold GlobalRegistry interfaces. Do not add managed delegates to transport surfaces.",
                    new Dictionary<string, object?> { ["isEditor"] = false });
            }
        }

        private void ScanManagedStringPayload(string relativePath, string rawLine, string code, int lineNumber, bool isEditor, StructMetadata? currentStruct, bool currentStructIsSignalCandidate, bool currentStructIsStrictRuntimeContract)
        {
            if (!currentStructIsSignalCandidate || !ContainsAny(code, "string", "String") || !StringFieldRegex.IsMatch(code))
            {
                return;
            }

            var symbol = currentStruct?.Name ?? "";
            var implementsISignal = currentStruct?.ImplementsISignal ?? false;
            if (isEditor)
            {
                AddFinding("WARN", "EDITOR_MANAGED_STRING_IN_SIGNAL_REVIEW", 60, "EDITOR_ONLY_REVIEW", "STRUCT_BODY_FIELD_SCAN", relativePath, lineNumber, symbol, rawLine, "Editor/test signal-like structs can use managed strings, but must not become runtime payload contracts.",
                    new Dictionary<string, object?> { ["isEditor"] = true, ["implementsISignal"] = implementsISignal });
            }
            else if (!currentStructIsStrictRuntimeContract)
            {
                AddFinding("WARN", "MANAGED_STRING_IN_SIGNAL_LIKE_REVIEW", 72, "STATIC_CONTRACT_REVIEW", "STRUCT_BODY_FIELD_SCAN", relativePath, lineNumber, symbol, rawLine, "This signal-like private/native-adjacent struct carries a managed string. Confirm it never crosses SignalBus<T>, NativeQueue<T>, Burst, or NativeArray boundaries; otherwise replace with FixedString or a stable uint hash.",
                    new Dictionary<string, object?> { ["isEditor"] = false, ["implementsISignal"] = implementsISignal, ["strictRuntimeContract"] = false });
            }
            else
            {
                AddFinding("ERROR", "MANAGED_STRING_IN_SIGNAL_PAYLOAD", 94, "CONFIRMED_OR_PROBABLE_RUNTIME_PAYLOAD", "STRUCT_BODY_FIELD_SCAN", relativePath, lineNumber, symbol, rawLine, "Use FixedString32Bytes/64Bytes or a stable uint hash inside signal payloads.",
                    new Dictionary<string, object?> { ["isEditor"] = false, ["implementsISignal"] = implementsISignal, ["strictRuntimeContract"] = true });
            }
        }

        private void ScanTransitivePack1Field(string relativePath, string rawLine, string code, int lineNumber, StructMetadata? currentStruct, bool currentStructIsStrictRuntimeContract)
        {
            if (!currentStructIsStrictRuntimeContract ||
                currentStruct is null ||
                !ContainsAny(code, "public", "internal", "private", "protected") ||
                _pack1StructsByName.Count == 0)
            {
                return;
            }

            var match = FieldDeclarationTypeRegex.Match(code);
            if (!match.Success)
            {
                return;
            }

            var fieldType = GetSimpleTypeName(match.Groups["type"].Value);
            if (fieldType == currentStruct.Name ||
                !_pack1StructsByName.TryGetValue(fieldType, out var pack1Infos))
            {
                return;
            }

            var fieldName = match.Groups["name"].Value;
            foreach (var pack1Info in pack1Infos)
            {
                _transitivePack1FieldCount++;
                AddFinding(
                    "WARN",
                    "TRANSITIVE_PACK1_FIELD_REVIEW",
                    pack1Info.HasWideField ? 88 : 82,
                    pack1Info.HasWideField ? "PROBABLE_ARM64_ALIGNMENT_RISK" : "STATIC_LAYOUT_REVIEW",
                    "STRUCT_BODY_FIELD_SCAN",
                    relativePath,
                    lineNumber,
                    currentStruct.Name + "." + fieldName,
                    rawLine,
                    "Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.",
                    new Dictionary<string, object?>
                    {
                        ["fieldType"] = fieldType,
                        ["pack1TypePath"] = pack1Info.Path,
                        ["pack1TypeLine"] = pack1Info.Line,
                        ["pack1TypeIsEditor"] = pack1Info.IsEditor,
                        ["pack1TypeFileFormatLike"] = pack1Info.IsFileFormatLike,
                        ["pack1TypeHasWideField"] = pack1Info.HasWideField,
                        ["currentStructPath"] = currentStruct.Path,
                        ["currentStructLine"] = currentStruct.Line
                    });
            }
        }

        private void ScanTelemetryRing(string relativePath, string rawText, string rawLine, string code, int lineNumber, bool isEditor, StructMetadata? currentStruct)
        {
            if (code.IndexOf("NativeArray", StringComparison.Ordinal) < 0 || !ContainsAny(code, "Telemetry", "BlackBox", "Signal"))
            {
                return;
            }

            var match = TelemetryArrayRegex.Match(code);
            if (!match.Success)
            {
                return;
            }

            var fieldName = match.Groups["name"].Value;
            var access = match.Groups["access"].Value;
            if (IsNativeTelemetryJobView(rawLine) ||
                (currentStruct is not null && (currentStruct.ImplementsBurstJob || currentStruct.HasExecuteMethod)))
            {
                AddFinding("INFO", "LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW", 54, "BORROWED_JOB_VIEW_REVIEW", "FIELD_DECLARATION_JOB_VIEW", relativePath, lineNumber, fieldName, rawLine, "This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.",
                    new Dictionary<string, object?> { ["isEditor"] = isEditor, ["borrowedView"] = true });
                return;
            }

            var ownership = GetOwnership(relativePath, rawText, rawLine, fieldName, "Array");
            var isTelemetryOrBlackBox = ContainsAny(code, "Telemetry", "BlackBox", "Blackbox") ||
                ContainsAny(fieldName, "Telemetry", "telemetry", "BlackBox", "blackBox", "Blackbox", "blackbox");
            if (!isTelemetryOrBlackBox)
            {
                AddFinding("INFO", "LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW", 68, "SIGNAL_SCRATCH_REVIEW", "FIELD_DECLARATION_PLUS_SENTINEL_SCAN", relativePath, lineNumber, fieldName, rawLine, "This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.",
                    ownership.ToTags(isEditor));
                return;
            }

            _localNativeTelemetryCount++;
            if (isEditor)
            {
                AddFinding("INFO", "EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW", 56, "EDITOR_ONLY_REVIEW", "FIELD_DECLARATION_PLUS_SENTINEL_SCAN", relativePath, lineNumber, fieldName, rawLine, "Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.",
                    ownership.ToTags(true));
            }
            else if (ownership.HasVaultAlias)
            {
                AddFinding("INFO", "LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS", 92, "CONFIRMED_VAULT_ALIAS_REVIEW", "FIELD_DECLARATION_PLUS_VAULT_ALIAS", relativePath, lineNumber, fieldName, rawLine, "This field is documented as a GlobalDataVault alias. Verify generation checks and dispose ownership stay in the vault; do not count it as a private owner breach.",
                    ownership.ToTags(isEditor));
            }
            else if (ownership.IsH8MemoryRootAllocator || IsGlobalDataVaultRoot(relativePath))
            {
                _registeredLocalTelemetryCount++;
                AddFinding("INFO", "LOCAL_NATIVE_TELEMETRY_RING_ROOT_OWNER", 91, "CONFIRMED_ROOT_ALLOCATOR_TELEMETRY", "FIELD_DECLARATION_PLUS_H8MEMORY_SCAN", relativePath, lineNumber, fieldName, rawLine, "This telemetry ring belongs to the H8Memory/GlobalDataVault root allocation layer. Keep dispose coverage, but do not classify the root owner itself as a downstream private non-vault breach.",
                    ownership.ToTags(isEditor));
            }
            else if (ownership.IsOwned)
            {
                _registeredLocalTelemetryCount++;
                if (IsTelemetryExportStagingBuffer(relativePath, rawText, fieldName))
                {
                    AddFinding("INFO", "LOCAL_NATIVE_TELEMETRY_STAGING_BUFFER_OWNER_LOCAL", 82, "CONFIRMED_OWNER_LOCAL_TELEMETRY_STAGING", "FIELD_DECLARATION_PLUS_SENTINEL_SCAN", relativePath, lineNumber, fieldName, rawLine, "This NativeArray is an owner-local telemetry export staging buffer, not persistent blackbox authority. Keep sentinel registration and do not migrate it unless another domain consumes the buffer directly.",
                        ownership.ToTags(isEditor));
                }
                else if (IsModApiOwnerLocalTelemetryRing(relativePath, rawText, fieldName))
                {
                    AddFinding("INFO", "MOD_API_NATIVE_TELEMETRY_RING_OWNER_LOCAL", 82, "CONFIRMED_MOD_API_OWNER_LOCAL_TELEMETRY", "FIELD_DECLARATION_PLUS_SENTINEL_SCAN", relativePath, lineNumber, fieldName, rawLine, "This NativeArray is an owner-local mod/API bridge telemetry ring with sentinel ownership and bounded lifetime. Keep it isolated from gameplay authority and do not migrate it to GlobalDataVault unless another runtime domain consumes it.",
                        ownership.ToTags(isEditor));
                }
                else if (IsOwnerLocalTelemetryRing(relativePath, rawText, fieldName))
                {
                    AddFinding("INFO", "LOCAL_NATIVE_TELEMETRY_RING_OWNER_LOCAL", 80, "CONFIRMED_OWNER_LOCAL_TELEMETRY", "FIELD_DECLARATION_PLUS_SENTINEL_SCAN", relativePath, lineNumber, fieldName, rawLine, "This telemetry/blackbox ring has sentinel ownership, bounded lifetime, and owner-local dump usage. Do not migrate it to GlobalDataVault unless another domain consumes the buffer or the state becomes persistent authority.",
                        ownership.ToTags(isEditor));
                }
                else
                {
                    AddFinding("WARN", "LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT", 88, "CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL", "FIELD_DECLARATION_PLUS_SENTINEL_SCAN", relativePath, lineNumber, fieldName, rawLine, "This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.",
                        ownership.ToTags(isEditor));
                }
            }
            else if (!ownership.HasAllocation)
            {
                if (currentStruct is not null && !string.Equals(access, "private", StringComparison.Ordinal))
                {
                    AddFinding("INFO", "LOCAL_NATIVE_TELEMETRY_STRUCT_VIEW_REVIEW", 56, "BORROWED_STRUCT_VIEW_REVIEW", "FIELD_DECLARATION_WITHOUT_ALLOCATION", relativePath, lineNumber, fieldName, rawLine, "This public/internal NativeArray telemetry field is inside a struct and has no same-file allocation. Treat it as a borrowed view unless source proves persistent ownership.",
                        ownership.ToTags(isEditor));
                    return;
                }

                AddFinding("WARN", "LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY", 73, "STATIC_DECLARATION_REVIEW", "FIELD_DECLARATION_WITHOUT_ALLOCATION", relativePath, lineNumber, fieldName, rawLine, "This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.",
                    ownership.ToTags(isEditor));
            }
            else
            {
                AddFinding("ERROR", "LOCAL_NATIVE_TELEMETRY_RING_UNOWNED", 90, "PROBABLE_NATIVE_OWNERSHIP_BREACH", "FIELD_DECLARATION_PLUS_SENTINEL_SCAN", relativePath, lineNumber, fieldName, rawLine, "Persistent telemetry/blackbox rings must be DataVault-owned or at least registered, unregistered, and disposed through the native sentinel.",
                    ownership.ToTags(isEditor));
            }
        }

        private void ScanLocalSignalQueue(string relativePath, string rawText, string rawLine, string code, int lineNumber, bool isEditor)
        {
            if (code.IndexOf("NativeQueue", StringComparison.Ordinal) < 0 || !ContainsAny(code, "Signal", "Command", "Packet"))
            {
                return;
            }

            var match = SignalQueueRegex.Match(code);
            if (!match.Success || Regex.IsMatch(relativePath, @"Core/GlobalSignals\.cs|Core/Signals/SignalWardenRuntime\.cs|Editor/"))
            {
                return;
            }

            _localNativeQueueCount++;
            var fieldName = match.Groups[2].Value;
            var ownership = GetOwnership(relativePath, rawText, rawLine, fieldName, "Queue");
            var bridgeContract = ClassifySignalQueueBridgeContract(rawText, rawLine, fieldName);
            var tags = ownership.ToTags(isEditor);
            bridgeContract.ApplyTo(tags);
            if (ownership.IsOwned)
            {
                if (bridgeContract.IsComplete)
                {
                    AddFinding("INFO", "LOCAL_SIGNAL_QUEUE_REGISTERED_BRIDGE_REVIEW", 70, "REGISTERED_LOCAL_QUEUE_REVIEW", "FIELD_DECLARATION_PLUS_SENTINEL_SCAN", relativePath, lineNumber, fieldName, rawLine, "This local signal queue has native ownership and bridge-lane contract tokens. Keep it out of first-party gameplay fan-out unless a current route card requires it.",
                        tags);
                }
                else
                {
                    AddFinding("WARN", "DIRECT_SIGNAL_QUEUE_BRIDGE_CONTRACT_INCOMPLETE", 84, "NATIVEQUEUE_SIGNAL_BRIDGE_CONTRACT_DEBT", "FIELD_DECLARATION_PLUS_CONTRACT_SCAN", relativePath, lineNumber, fieldName, rawLine, "Retained direct NativeQueue signal lanes need owner, drain phase, max frame budget/capacity, deterministic overflow/coalescing, and telemetry counter. New gameplay fan-out must use SignalBus<T>.",
                        tags);
                }
            }
            else if (!ownership.HasAllocation)
            {
                AddFinding(bridgeContract.IsComplete ? "INFO" : "WARN", "LOCAL_SIGNAL_QUEUE_DECLARED_ONLY_REVIEW", bridgeContract.IsComplete ? 61 : 78, bridgeContract.IsComplete ? "STATIC_DECLARATION_REVIEW" : "NATIVEQUEUE_SIGNAL_BRIDGE_CONTRACT_DEBT", "FIELD_DECLARATION_WITHOUT_ALLOCATION", relativePath, lineNumber, fieldName, rawLine, "This NativeQueue field has no allocation in the same source file. If it is retained as a bridge lane, keep owner/drain/budget/overflow/telemetry visible beside the declaration or allocator.",
                    tags);
            }
            else
            {
                AddFinding("ERROR", "DIRECT_SIGNAL_QUEUE_UNOWNED_OR_CONTRACTLESS", 90, "PROBABLE_SIGNAL_CORRIDOR_BYPASS", "FIELD_DECLARATION_PLUS_SENTINEL_SCAN", relativePath, lineNumber, fieldName, rawLine, "Direct runtime NativeQueue signal lanes must not bypass SignalBus<T> without sentinel ownership and a bridge contract. Register ownership or migrate producers to SignalBus<T>.",
                    tags);
            }
        }

        private void ScanGlobalSignalsDirectUse(string relativePath, string rawLine, string code, int lineNumber, bool isEditor, string methodName)
        {
            if (isEditor || code.IndexOf("GlobalSignals.", StringComparison.Ordinal) < 0)
            {
                return;
            }

            if (Regex.IsMatch(relativePath, @"Core/GlobalSignals\.cs$|Core/Signals/"))
            {
                return;
            }

            var match = GlobalSignalsDirectUseRegex.Match(code);
            if (!match.Success)
            {
                return;
            }

            var member = match.Groups["member"].Value;
            if (string.Equals(member, "Publish", StringComparison.Ordinal))
            {
                AddFinding("ERROR", "DIRECT_GLOBALSIGNALS_PUBLISH_RUNTIME", 92, "CONFIRMED_SIGNAL_CORRIDOR_BYPASS", "GLOBALSIGNALS_MEMBER_SCAN", relativePath, lineNumber, member, rawLine, "First-party runtime publishes must use typed SignalBus<T> lanes or documented bridge queues. Do not publish gameplay traffic directly through GlobalSignals.",
                    new Dictionary<string, object?> { ["isEditor"] = false, ["member"] = member, ["method"] = methodName });
                return;
            }

            if (IsHotMethodName(methodName))
            {
                AddFinding("WARN", "HOT_GLOBALSIGNALS_RUNTIME_LOOKUP_REVIEW", 82, "HOT_PATH_GLOBAL_SIGNAL_BRIDGE", "GLOBALSIGNALS_MEMBER_SCAN", relativePath, lineNumber, member, rawLine, "Hot paths must not poll GlobalSignals as a runtime context source. Cache owner snapshots during bootstrap/owner phase or consume SignalBus<T> frame snapshots.",
                    new Dictionary<string, object?> { ["isEditor"] = false, ["member"] = member, ["method"] = methodName });
                return;
            }

            AddFinding("INFO", "GLOBALSIGNALS_BRIDGE_REVIEW", 58, "LEGACY_SIGNAL_BRIDGE_REVIEW", "GLOBALSIGNALS_MEMBER_SCAN", relativePath, lineNumber, member, rawLine, "GlobalSignals direct access is legacy/bridge infrastructure only. Keep the owner, phase, and migration path explicit.",
                new Dictionary<string, object?> { ["isEditor"] = false, ["member"] = member, ["method"] = methodName });
        }

        private static bool IsGlobalDataVaultRoot(string relativePath)
        {
            return relativePath.EndsWith("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs", StringComparison.Ordinal);
        }

        private static bool IsOwnerLocalTelemetryRing(string relativePath, string rawText, string fieldName)
        {
            if (Regex.IsMatch(relativePath, @"GlobalTelemetryBus|ModdingAPI"))
            {
                return false;
            }

            var escaped = BuildNativeFieldAliasPattern(rawText, fieldName, "Array");
            var hasBoundedLifetimeRegistration = Regex.IsMatch(
                rawText,
                @"RegisterNativeArray\s*\([^;]*" + escaped + @"[^;]*(NativeAllocationLifetime\.Scene|NativeAllocationLifetime\.Session|NativeMemoryLifetime|NativeMemoryBridgeLifetime)",
                RegexOptions.Singleline);
            var hasHelperBoundedLifetimeRegistration =
                IsAssignedByArrayAllocatorHelper(rawText, escaped) &&
                Regex.IsMatch(rawText, @"RegisterNativeArray\s*\([^;]*(NativeAllocationLifetime\.Scene|NativeAllocationLifetime\.Session|NativeMemoryLifetime)", RegexOptions.Singleline);
            var hasOwnerDumpRoute = Regex.IsMatch(
                rawText,
                @"Dump[A-Za-z0-9_]*(Telemetry|Black[Bb]ox)|TelemetryDumpRelativePath|Dump_[A-Za-z0-9_]+\.bin",
                RegexOptions.Singleline);
            var exposesReadOnlyAccessor = Regex.IsMatch(
                rawText,
                @"public\s+NativeArray\s*<[^>]+>\.ReadOnly\s+[A-Za-z_][A-Za-z0-9_]*\s*=>\s*" + escaped,
                RegexOptions.Singleline);
            return (hasBoundedLifetimeRegistration || hasHelperBoundedLifetimeRegistration) &&
                hasOwnerDumpRoute &&
                !exposesReadOnlyAccessor;
        }

        private static bool IsModApiOwnerLocalTelemetryRing(string relativePath, string rawText, string fieldName)
        {
            if (!relativePath.EndsWith("Assets/_Project/Scripts/ModdingAPI/ModEventProjectionBridge.cs", StringComparison.Ordinal) ||
                !string.Equals(fieldName, "_cullTelemetry", StringComparison.Ordinal))
            {
                return false;
            }

            var escaped = BuildNativeFieldAliasPattern(rawText, fieldName, "Array");
            return rawText.Contains("IHectonEventChannel", StringComparison.Ordinal) &&
                rawText.Contains("local bridge-owned memory to avoid DataVault hot writes", StringComparison.Ordinal) &&
                Regex.IsMatch(rawText, @"RegisterNativeArray\s*\([^;]*" + escaped + @"[^;]*NativeAllocationLifetime\.Session", RegexOptions.Singleline) &&
                Regex.IsMatch(rawText, @"UnregisterNativeArray\s*\([^;]*" + escaped + @"[^;]*\).*?Dispose\s*\(\s*\)", RegexOptions.Singleline);
        }

        private static bool IsTelemetryExportStagingBuffer(string relativePath, string rawText, string fieldName)
        {
            if (!relativePath.EndsWith("Assets/_Project/Scripts/Core/GlobalTelemetryBus.cs", StringComparison.Ordinal) ||
                !fieldName.Contains("snapshot", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var escaped = BuildNativeFieldAliasPattern(rawText, fieldName, "Array");
            return Regex.IsMatch(rawText, @"RegisterNativeArray\s*\([^;]*" + escaped, RegexOptions.Singleline) &&
                rawText.Contains("CopyRange", StringComparison.Ordinal) &&
                rawText.Contains("PrepareExportState", StringComparison.Ordinal) &&
                rawText.Contains("GetUnsafeReadOnlyPtr", StringComparison.Ordinal) &&
                !Regex.IsMatch(rawText, @"public\s+NativeArray\s*<[^>]+>\.ReadOnly\s+[A-Za-z_][A-Za-z0-9_]*\s*=>\s*" + escaped, RegexOptions.Singleline);
        }

        private static bool IsNativeTelemetryJobView(string rawLine)
        {
            return rawLine.Contains("[NoAlias]", StringComparison.Ordinal) ||
                rawLine.Contains("[ReadOnly", StringComparison.Ordinal);
        }

        private void ScanSyncRuntimeIo(string relativePath, string rawLine, string code, int lineNumber, bool isEditor, string methodName)
        {
            if (!ContainsAny(code, "File", "Directory") ||
                isEditor ||
                Regex.IsMatch(relativePath, "Save|Persistence|Crash|Dump|Telemetry|Tools|Editor") ||
                !SyncIoRegex.IsMatch(code))
            {
                return;
            }

            if (IsColdOrFatalIoContext(relativePath, methodName))
            {
                _coldSyncIoCount++;
                AddFinding("INFO", "COLD_OR_FATAL_SYNC_IO_REVIEW", 64, "COLD_OR_FATAL_IO_BOUNDARY", "SANITIZED_LINE_REGEX", relativePath, lineNumber, methodName, rawLine, "This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.",
                    new Dictionary<string, object?> { ["isEditor"] = false, ["method"] = methodName });
                return;
            }

            AddFinding("WARN", "RUNTIME_SYNC_FILE_IO_REVIEW", 76, "IO_PRESSURE_HEURISTIC", "SANITIZED_LINE_REGEX", relativePath, lineNumber, "", rawLine, "Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.",
                new Dictionary<string, object?> { ["isEditor"] = false, ["method"] = methodName });
        }

        private void ScanHotPathHeuristics(string relativePath, string rawLine, string code, int lineNumber, bool isEditor, string methodName)
        {
            if (isEditor || methodName.Length == 0 || !IsHotMethodName(methodName))
            {
                return;
            }

            if (rawLine.Contains("COLD ALLOC:", StringComparison.Ordinal) || IsFieldDeclarationLike(code))
            {
                return;
            }

            if (ContainsAny(code, "foreach", ".Where", ".Select", ".OrderBy", ".ToList", ".ToArray", "Enumerable") &&
                HotPathEnumerationRegex.IsMatch(code))
            {
                _hotPathRiskCount++;
                AddFinding("WARN", "ZERO_GC_HOT_PATH_ENUMERATION_REVIEW", 72, "HOT_PATH_HEURISTIC", "HOT_METHOD_REGEX", relativePath, lineNumber, methodName, rawLine, "Review this hot-path enumeration/LINQ surface for allocations, boxing, or hidden iterator state.",
                    new Dictionary<string, object?> { ["isEditor"] = false, ["method"] = methodName });
            }

            if (code.Contains("new ", StringComparison.Ordinal) &&
                HotPathAllocationRegex.IsMatch(code) &&
                !Regex.IsMatch(code, @"new\s+(NativeArray|NativeList|NativeQueue|NativeHashMap|NativeParallel|UnsafeList|UnsafeHashMap)\b"))
            {
                _hotPathRiskCount++;
                if (IsColdAllocationReviewContext(relativePath, methodName, code))
                {
                    AddFinding("INFO", "COLD_OR_ASYNC_ALLOCATION_REVIEW", 58, "COLD_OR_ASYNC_ALLOCATION_BOUNDARY", "HOT_METHOD_NAME_WITH_COLD_CONTEXT", relativePath, lineNumber, methodName, rawLine, "This allocation is inside a cold/save/telemetry/async context despite a hot-looking method name. Keep it outside frame cadence; do not count it as a proven zero-GC hot-path breach without call-cadence proof.",
                        new Dictionary<string, object?> { ["isEditor"] = false, ["method"] = methodName });
                }
                else
                {
                    AddFinding("WARN", "ZERO_GC_HOT_PATH_ALLOCATION_REVIEW", 66, "HOT_PATH_HEURISTIC", "HOT_METHOD_REGEX", relativePath, lineNumber, methodName, rawLine, "Review this hot-path allocation. If intentional, move it to bootstrap/cold path or document the pooled owner.",
                        new Dictionary<string, object?> { ["isEditor"] = false, ["method"] = methodName });
                }
            }

            if (ContainsAny(code, "GetComponent", "FindObject", "GameObject.Find", "Object.Find") &&
                UnityLookupRegex.IsMatch(code))
            {
                _hotPathRiskCount++;
                AddFinding("WARN", "HOT_PATH_UNITY_LOOKUP_REVIEW", 82, "HOT_PATH_HEURISTIC", "HOT_METHOD_REGEX", relativePath, lineNumber, methodName, rawLine, "Cache component/object references outside Tick/Update/Schedule paths. Do not perform Unity hierarchy lookups in hot loops.",
                    new Dictionary<string, object?> { ["isEditor"] = false, ["method"] = methodName });
            }

            if (code.Contains("GlobalRegistry", StringComparison.Ordinal) &&
                GlobalRegistryHotLookupRegex.IsMatch(code))
            {
                _hotPathRiskCount++;
                AddFinding("ERROR", "HOT_PATH_GLOBALREGISTRY_LOOKUP_FORBIDDEN", 94, "HOT_PATH_DEPENDENCY_POLLING", "HOT_METHOD_REGEX", relativePath, lineNumber, methodName, rawLine, "GlobalRegistry is cold dependency injection only. Cache interfaces during bootstrap/owner phases and consume immutable snapshots in Tick/Update/Execute paths.",
                    new Dictionary<string, object?> { ["isEditor"] = false, ["method"] = methodName });
            }

            if (ContainsAny(code, ".Complete(", "CompleteAll(") && JobCompleteRegex.IsMatch(code))
            {
                _hotPathRiskCount++;
                AddFinding("ERROR", "HOT_PATH_JOB_COMPLETE_FORBIDDEN", 94, "HOT_PATH_SYNC_STALL", "HOT_METHOD_REGEX", relativePath, lineNumber, methodName, rawLine, "Do not complete jobs inside Tick/Update/Execute-style methods. Route completion through dispatcher-owned PostSimulation/VisualSync swap windows or a documented blocking sync point.",
                    new Dictionary<string, object?> { ["isEditor"] = false, ["method"] = methodName });
            }

            if (code.Contains(".GetData", StringComparison.Ordinal) && GetDataRegex.IsMatch(code))
            {
                _hotPathRiskCount++;
                AddFinding("ERROR", "HOT_PATH_GPU_GETDATA_FORBIDDEN", 94, "HOT_PATH_GPU_CPU_STALL", "HOT_METHOD_REGEX", relativePath, lineNumber, methodName, rawLine, "Synchronous runtime readback blocks CPU/GPU overlap. Use delayed AsyncGPUReadback with a ring-buffered telemetry/query lane.",
                    new Dictionary<string, object?> { ["isEditor"] = false, ["method"] = methodName });
            }

            if (code.Contains(".SetData", StringComparison.Ordinal) && SetDataRegex.IsMatch(code))
            {
                if (IsNonGpuBurstJobSetupSetData(relativePath, methodName, code))
                {
                    AddFinding("INFO", "BURST_JOB_SETUP_SETDATA_REVIEW", 58, "NON_GPU_JOB_SETUP_SETDATA", "HOT_METHOD_REGEX", relativePath, lineNumber, methodName, rawLine, "This SetData call initializes a local Burst job struct from native aliases/scalars before scheduling. It is not a GPU upload; keep it allocation-free and do not suppress real buffer SetData warnings globally.",
                        new Dictionary<string, object?> { ["isEditor"] = false, ["method"] = methodName });
                }
                else
                {
                    _hotPathRiskCount++;
                    AddFinding("WARN", "HOT_PATH_SETDATA_REVIEW", 82, "HOT_PATH_UPLOAD_STALL_RISK", "HOT_METHOD_REGEX", relativePath, lineNumber, methodName, rawLine, "Review SetData inside a hot method. GPU uploads must use frame-start budgeted dirty ranges or LockBufferForWrite+MemCpy; non-GPU SetData APIs need an explicit cold-path or non-alloc proof.",
                        new Dictionary<string, object?> { ["isEditor"] = false, ["method"] = methodName });
                }
            }

            if (ContainsAny(code, ".material", "Material.Set", ".SetFloat", ".SetColor", ".SetVector", ".SetTexture") &&
                TryClassifyMaterialHotPathMutation(code, out var usesPropertyBlock, out var usesComputeShader))
            {
                _hotPathRiskCount++;
                if (usesComputeShader)
                {
                    AddFinding("INFO", "GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW", 54, "HOT_PATH_HEURISTIC", "HOT_METHOD_REGEX", relativePath, lineNumber, methodName, rawLine, "This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.",
                        new Dictionary<string, object?> { ["isEditor"] = false, ["method"] = methodName, ["computeShaderLike"] = true });
                }
                else if (usesPropertyBlock)
                {
                    AddFinding("INFO", "MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW", 52, "HOT_PATH_HEURISTIC", "HOT_METHOD_REGEX", relativePath, lineNumber, methodName, rawLine, "This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.",
                        new Dictionary<string, object?> { ["isEditor"] = false, ["method"] = methodName, ["propertyBlockLike"] = true });
                }
                else
                {
                    AddFinding("WARN", "SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW", 64, "HOT_PATH_HEURISTIC", "HOT_METHOD_REGEX", relativePath, lineNumber, methodName, rawLine, "Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.",
                        new Dictionary<string, object?> { ["isEditor"] = false, ["method"] = methodName, ["propertyBlockLike"] = false });
                }
            }
        }

        private static bool IsNonGpuBurstJobSetupSetData(string relativePath, string methodName, string code)
        {
            return relativePath.EndsWith("Assets/_Project/Scripts/World/GlobalWorldSampler.cs", StringComparison.Ordinal) &&
                methodName.StartsWith("Schedule", StringComparison.Ordinal) &&
                Regex.IsMatch(code, @"\bjob\s*\.\s*SetData\s*\(\s*in\s+data\s*\)");
        }

        private void ScanComputeFile(string path)
        {
            var relativePath = ToRelativePath(path);
            var lines = File.ReadAllLines(path);
            _shaderFilesScanned++;
            for (var i = 0; i < lines.Length; i++)
            {
                if (!Compute1024Regex.IsMatch(lines[i]))
                {
                    continue;
                }

                _computeThreadGroupRiskCount++;
                AddFinding("WARN", "COMPUTE_THREADS_1024_REVIEW", 80, "GPU_PORTABILITY_HEURISTIC", "COMPUTE_SHADER_SCAN", relativePath, i + 1, "", lines[i], "Use tiered thread-group constants; 1024-wide groups are PC-biased and risky on mobile/Metal-class GPUs.",
                    new Dictionary<string, object?> { ["shader"] = true });
            }
        }

        private void AddDuplicateFindings()
        {
            foreach (var group in _signalDefinitions.GroupBy(item => item.Name).Where(item => item.Count() > 1))
            {
                var entries = group.ToArray();
                var runtimeCount = entries.Count(item => !item.IsEditor);
                var strictRuntimeCount = entries.Count(item => item.IsStrictRuntimeContract);
                var editorCount = entries.Length - runtimeCount;
                foreach (var entry in entries)
                {
                    var tags = new Dictionary<string, object?>
                    {
                        ["duplicateCount"] = entries.Length,
                        ["runtimeDuplicateCount"] = runtimeCount,
                        ["strictRuntimeDuplicateCount"] = strictRuntimeCount,
                        ["editorDuplicateCount"] = editorCount
                    };

                    if (strictRuntimeCount > 1 && entry.IsStrictRuntimeContract)
                    {
                        AddFinding("ERROR", "DUPLICATE_RUNTIME_SIGNAL_NAME", 92, "CONFIRMED_RUNTIME_CONTRACT_COLLISION", "ANCHORED_STRUCT_GROUP", entry.Path, entry.Line, entry.Name, "struct " + entry.Name, "Signal names must be globally unique across runtime contracts. Merge duplicate contracts or wrap mock/domain-local payloads behind explicit names.", tags);
                    }
                    else if (runtimeCount >= 1 && entry.IsEditor)
                    {
                        AddFinding("WARN", "EDITOR_SIGNAL_NAME_SHADOWS_RUNTIME", 68, "EDITOR_ONLY_REVIEW", "ANCHORED_STRUCT_GROUP", entry.Path, entry.Line, entry.Name, "struct " + entry.Name, "Editor/test structs should not shadow runtime signal names; rename smoke payloads or fully isolate them.", tags);
                    }
                    else
                    {
                        AddFinding("WARN", "DUPLICATE_SIGNAL_LIKE_NAME_REVIEW", 74, "STATIC_CONTRACT_REVIEW", "ANCHORED_STRUCT_GROUP", entry.Path, entry.Line, entry.Name, "struct " + entry.Name, "Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.", tags);
                    }
                }
            }
        }

        private AuditResult BuildResult()
        {
            var ruleStats = _findings
                .GroupBy(item => item.Rule)
                .OrderByDescending(item => item.Count())
                .Select(item =>
                {
                    var findings = item.ToArray();
                    return new RuleStat(
                        item.Key,
                        findings.Length,
                        findings.Count(finding => finding.Severity == "ERROR"),
                        findings.Count(finding => finding.Severity == "WARN"),
                        findings.Count(finding => finding.Severity == "INFO"),
                        Math.Round(findings.Average(finding => finding.Confidence), 1));
                })
                .ToArray();

            var classificationStats = _findings
                .GroupBy(item => item.Classification)
                .OrderByDescending(item => item.Count())
                .Select(item => new ClassificationStat(item.Key, item.Count()))
                .ToArray();

            var errors = _findings.Count(item => item.Severity == "ERROR");
            var warnings = _findings.Count(item => item.Severity == "WARN");
            var infos = _findings.Count(item => item.Severity == "INFO");
            var confirmedErrors = _findings.Count(item => item.Severity == "ERROR" && item.Confidence >= 90);
            var reviewOnly = _findings.Count(item => item.Confidence < 75);
            var coreGlobalSignals = _signalDefinitions.Count(item => item.InCoreGlobalSignals);
            var signalsWithoutLayout = _signalDefinitions.Count(item => !item.HasStructLayout);

            return new AuditResult(
                Agent,
                "STATIC_SOURCE_CLASSIFIED",
                _options.Scope,
                DateTime.UtcNow.ToString("o"),
                _options.ProjectRoot,
                _scannedFiles,
                _shaderFilesScanned,
                _pack1Count,
                _runtimeSignalPack1Count,
                _transitivePack1FieldCount,
                _managedEventCount,
                _localNativeTelemetryCount,
                _registeredLocalTelemetryCount,
                _localNativeQueueCount,
                _computeThreadGroupRiskCount,
                _hotPathRiskCount,
                _coldSyncIoCount,
                _asmdefContractBoundaryCount,
                _cacheLineCriticalStrideDebtCount,
                _signalDefinitions.Count,
                coreGlobalSignals,
                signalsWithoutLayout,
                errors,
                warnings,
                infos,
                confirmedErrors,
                reviewOnly,
                ruleStats,
                classificationStats,
                _findings.ToArray());
        }

        private void AddFinding(string severity, string rule, int confidence, string classification, string evidenceKind, string path, int line, string symbol, string evidence, string requiredAction, Dictionary<string, object?> tags)
        {
            _findings.Add(new Finding(severity, rule, Math.Clamp(confidence, 1, 100), classification, evidenceKind, path, line, symbol, evidence.Trim(), requiredAction, tags));
        }

        private string ToRelativePath(string path)
        {
            var full = Path.GetFullPath(path);
            var root = _options.ProjectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return full[root.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/');
            }

            return full.Replace('\\', '/');
        }

        private static bool UsesSignalContracts(string text)
        {
            return text.Contains("using Hecton8.Core.Contracts.Signals", StringComparison.Ordinal) ||
                   text.Contains("Hecton8.Core.Contracts.Signals.", StringComparison.Ordinal) ||
                   Regex.IsMatch(text, @":\s*(?:[^{};,\n]*\.)?ISignal\b");
        }

        private AsmdefInfo? TryReadAsmdef(string path)
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var root = document.RootElement;
                if (!root.TryGetProperty("name", out var nameElement))
                {
                    return null;
                }

                var references = new HashSet<string>(StringComparer.Ordinal);
                if (root.TryGetProperty("references", out var referencesElement) &&
                    referencesElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var reference in referencesElement.EnumerateArray())
                    {
                        if (reference.ValueKind == JsonValueKind.String)
                        {
                            var value = reference.GetString();
                            if (!string.IsNullOrWhiteSpace(value))
                            {
                                references.Add(value);
                            }
                        }
                    }
                }

                var rawLines = File.ReadAllLines(path);
                var referenceLine = 1;
                for (var i = 0; i < rawLines.Length; i++)
                {
                    if (rawLines[i].Contains("\"references\"", StringComparison.Ordinal))
                    {
                        referenceLine = i + 1;
                        break;
                    }
                }

                return new AsmdefInfo(
                    path,
                    ToRelativePath(path),
                    nameElement.GetString() ?? "",
                    references,
                    Path.GetDirectoryName(Path.GetFullPath(path)) ?? _options.ProjectRoot,
                    referenceLine);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static AsmdefInfo? FindNearestAsmdef(string sourcePath, List<AsmdefInfo> asmdefs)
        {
            var full = Path.GetFullPath(sourcePath);
            AsmdefInfo? nearest = null;
            var nearestLength = -1;
            foreach (var asmdef in asmdefs)
            {
                if (IsUnderDirectory(full, asmdef.Directory) && asmdef.Directory.Length > nearestLength)
                {
                    nearest = asmdef;
                    nearestLength = asmdef.Directory.Length;
                }
            }

            return nearest;
        }

        private static bool IsUnderDirectory(string path, string directory)
        {
            if (!path.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return path.Length == directory.Length ||
                   path[directory.Length] == Path.DirectorySeparatorChar ||
                   path[directory.Length] == Path.AltDirectorySeparatorChar;
        }
    }

    private static bool HasRelevantText(string rawText)
    {
        return ContainsAny(rawText,
            "struct",
            "StructLayout",
            "NativeArray",
            "NativeQueue",
            "NativeList",
            "SignalBus",
            "GlobalSignals.",
            "UnityEvent",
            "Action",
            "Func",
            "SendMessage",
            "BroadcastMessage",
            "File.",
            "Directory.",
            "FileStream",
            "GetComponent",
            "FindObject",
            "GameObject.Find",
            "Object.Find",
            ".Complete(",
            "CompleteAll(",
            ".GetData",
            ".SetData",
            ".material",
            "Material.Set",
            ".SetFloat",
            ".SetColor",
            ".SetVector",
            ".SetTexture");
    }

    private static string RemoveCodeTrivia(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return "";
        }

        var inString = false;
        var inChar = false;
        var verbatim = false;
        for (var i = 0; i < line.Length - 1; i++)
        {
            var c = line[i];
            var next = line[i + 1];

            if (!inChar && c == '"' && (i == 0 || line[i - 1] != '\\'))
            {
                verbatim = i > 0 && line[i - 1] == '@';
                inString = !inString;
                continue;
            }

            if (!inString && c == '\'' && (i == 0 || line[i - 1] != '\\'))
            {
                inChar = !inChar;
                continue;
            }

            if (inString && verbatim && c == '"' && next == '"')
            {
                i++;
                continue;
            }

            if (!inString && !inChar && c == '/' && next == '/')
            {
                return line[..i];
            }
        }

        return line;
    }

    private static bool IsEditorPath(string relativePath)
    {
        return Regex.IsMatch(relativePath, @"(^|/)Editor(/|$)|(^|/)Tests/Editor(/|$)|SmokeTester|SmokeTest|Automation|QA/Headless|TOOL_");
    }

    private static bool IsCoreSignalFile(string relativePath)
    {
        return Regex.IsMatch(relativePath, @"Core/GlobalSignals\.cs$|Core/Signals/");
    }

    private static bool TryUpdatePreprocessorFrame(string code, List<PreprocessorFrame> frames)
    {
        var trimmed = code.TrimStart();
        if (!trimmed.StartsWith('#'))
        {
            return false;
        }

        if (trimmed.StartsWith("#if", StringComparison.Ordinal) &&
            !trimmed.StartsWith("#ifdef", StringComparison.Ordinal) &&
            !trimmed.StartsWith("#ifndef", StringComparison.Ordinal))
        {
            var condition = trimmed[3..].Trim();
            frames.Add(new PreprocessorFrame(condition, IsEditorOnlyCondition(condition)));
            return true;
        }

        if (trimmed.StartsWith("#elif", StringComparison.Ordinal))
        {
            if (frames.Count > 0)
            {
                var condition = trimmed[5..].Trim();
                var current = frames[^1];
                frames[^1] = current with { IsCurrentBranchEditorOnly = IsEditorOnlyCondition(condition) };
            }

            return true;
        }

        if (trimmed.StartsWith("#else", StringComparison.Ordinal))
        {
            if (frames.Count > 0)
            {
                var current = frames[^1];
                frames[^1] = current with { IsCurrentBranchEditorOnly = IsNonEditorOnlyCondition(current.RootCondition) };
            }

            return true;
        }

        if (trimmed.StartsWith("#endif", StringComparison.Ordinal))
        {
            if (frames.Count > 0)
            {
                frames.RemoveAt(frames.Count - 1);
            }

            return true;
        }

        return false;
    }

    private static bool IsInsideEditorOnlyPreprocessor(List<PreprocessorFrame> frames)
    {
        for (var i = 0; i < frames.Count; i++)
        {
            if (frames[i].IsCurrentBranchEditorOnly)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEditorOnlyCondition(string condition)
    {
        var normalized = Regex.Replace(condition, @"\s+", "");
        return normalized.Contains("UNITY_EDITOR", StringComparison.Ordinal) &&
            !normalized.Contains("||", StringComparison.Ordinal) &&
            !normalized.Contains("!UNITY_EDITOR", StringComparison.Ordinal);
    }

    private static bool IsNonEditorOnlyCondition(string condition)
    {
        var normalized = Regex.Replace(condition, @"\s+", "");
        return normalized.Contains("!UNITY_EDITOR", StringComparison.Ordinal) &&
            !normalized.Contains("||", StringComparison.Ordinal);
    }

    private static bool IsFileFormatLike(string relativePath, string symbol)
    {
        var combined = relativePath + "/" + symbol;
        return Regex.IsMatch(combined, "Save|Persistence|Persist|Serialize|Deserialize|Binary|Codec|Compression|Archive|Header|Record|Wal|WAL|Pager|Page|Snapshot|Modding|Protocol|Manifest|Layout|Disk|FileFormat|StaticData|DataArena");
    }

    private static bool IsSignalLikeName(string symbol)
    {
        return Regex.IsMatch(symbol, "(Signal|Command|Packet|Telemetry|BlackBox|Aup|AbsoluteUniversePosition)$|Telemetry|BlackBox");
    }

    private static bool IsHotMethodName(string methodName)
    {
        if (Regex.IsMatch(methodName, "(Cold|Benchmark|Bootstrap|Initialize|Initialise|Resolve|Cache|Bind|Rebind|Awake|OnEnable|Start)", RegexOptions.IgnoreCase))
        {
            return false;
        }

        return Regex.IsMatch(methodName, @"^(Tick|Update|LateUpdate|FixedUpdate|Execute|OnUpdate|Run|Schedule|Simulate|Step|Process|Dispatch|Flush|Render|Sync)");
    }

    private static bool TryStartPendingMethodDeclaration(string code, out string methodName, out bool isConstructor)
    {
        methodName = "";
        isConstructor = false;
        if (code.Contains("=>", StringComparison.Ordinal) ||
            Regex.IsMatch(code, @"\b(class|struct|interface|enum|if|for|foreach|while|switch|catch|using|lock)\b"))
        {
            return false;
        }

        var constructorMatch = ConstructorDeclarationStartRegex.Match(code);
        if (constructorMatch.Success)
        {
            methodName = constructorMatch.Groups["name"].Value;
            isConstructor = true;
            return true;
        }

        var methodMatch = MethodDeclarationStartRegex.Match(code);
        if (!methodMatch.Success)
        {
            return false;
        }

        methodName = methodMatch.Groups["name"].Value;
        return true;
    }

    private static bool IsColdAllocationReviewContext(string relativePath, string methodName, string code)
    {
        if (methodName.EndsWith("Async", StringComparison.Ordinal) ||
            methodName.Contains("Async", StringComparison.Ordinal))
        {
            return true;
        }

        if (code.Contains("FileStream", StringComparison.Ordinal) &&
            Regex.IsMatch(relativePath + "/" + methodName, "Save|Persistence|Telemetry|Black[Bb]ox|Dump|Crash|WAL|Wal|Flush|Load|Write|Read"))
        {
            return true;
        }

        return false;
    }

    private static bool TryClassifyMaterialHotPathMutation(string code, out bool usesPropertyBlock, out bool usesComputeShader)
    {
        usesPropertyBlock = false;
        usesComputeShader = false;
        if (code.Contains(".material", StringComparison.Ordinal))
        {
            return true;
        }

        if (!MaterialMutationRegex.IsMatch(code))
        {
            return false;
        }

        var match = Regex.Match(code, @"(?<receiver>[A-Za-z_][A-Za-z0-9_]*)\s*\.\s*Set(?:Float|Int|Color|Vector|Texture)\s*\(");
        if (!match.Success)
        {
            return true;
        }

        var receiver = match.Groups["receiver"].Value;
        usesComputeShader =
            receiver.Contains("Compute", StringComparison.Ordinal) ||
            receiver.Contains("compute", StringComparison.Ordinal);
        usesPropertyBlock =
            receiver.Contains("MPB", StringComparison.Ordinal) ||
            receiver.Contains("mpb", StringComparison.Ordinal) ||
            receiver.Contains("PropertyBlock", StringComparison.Ordinal) ||
            receiver.EndsWith("Block", StringComparison.Ordinal);
        return true;
    }

    private static bool IsColdOrFatalIoContext(string relativePath, string methodName)
    {
        var context = relativePath + "/" + methodName;
        return Regex.IsMatch(context, "Archaeology|Bake|Black[Bb]ox|Bootstrap|Cache|Cold|Crash|Csv|CSV|Debug|Diagnostic|Dump|Export|Fatal|FileWorker|Import|Initialize|Load|Open|Persistence|Report|Save|Shutdown|Stage|StaticData|Storage|Streaming|Telemetry|Teardown|TryLoad|TryOpen|Validate|WAL|Wal|Worker");
    }

    private static bool IsFieldDeclarationLike(string code)
    {
        return Regex.IsMatch(code, @"^\s*(?:public|internal|private|protected)\s+(?:static\s+)?(?:readonly\s+)?(?:volatile\s+)?(?:unsafe\s+)?[A-Za-z_][^;=]*\s+[A-Za-z_][A-Za-z0-9_]*\s*=");
    }

    private static string GetSimpleTypeName(string typeName)
    {
        var dot = typeName.LastIndexOf('.');
        return dot >= 0 ? typeName[(dot + 1)..] : typeName;
    }

    private static bool HasStructLayoutBefore(string[] codeLines, int index)
    {
        var start = Math.Max(0, index - 8);
        for (var i = index; i >= start; i--)
        {
            if (StructLayoutAttributeRegex.IsMatch(codeLines[i]))
            {
                return true;
            }

            if (i != index && Regex.IsMatch(codeLines[i], @"^\s*(?:public|internal|private|protected)?\s*(?:class|interface|enum)\s+"))
            {
                return false;
            }
        }

        return false;
    }

    private static int StructLayoutSizeBefore(string[] codeLines, int index)
    {
        var builder = new StringBuilder();
        var start = Math.Max(0, index - 8);
        for (var i = start; i <= index; i++)
        {
            if (codeLines[i].Contains("[StructLayout", StringComparison.Ordinal))
            {
                builder.Clear();
            }

            if (builder.Length > 0 || codeLines[i].Contains("[StructLayout", StringComparison.Ordinal))
            {
                builder.Append(' ');
                builder.Append(codeLines[i]);
                if (codeLines[i].Contains(']'))
                {
                    break;
                }
            }
        }

        var match = Regex.Match(builder.ToString(), @"\bSize\s*=\s*(\d+)");
        return match.Success && int.TryParse(match.Groups[1].Value, out var size) ? size : 0;
    }

    private static ForwardStatement BuildForwardStatement(string[] codeLines, int index, int maxContinuation = 12)
    {
        var builder = new StringBuilder();
        var endIndex = index;
        var limit = Math.Min(codeLines.Length - 1, index + maxContinuation);
        for (var i = index; i <= limit; i++)
        {
            builder.Append(' ');
            builder.Append(codeLines[i]);
            endIndex = i;
            if (codeLines[i].IndexOf(';') >= 0)
            {
                break;
            }
        }

        return new ForwardStatement(builder.ToString(), endIndex);
    }

    private static string BuildRawStatementEvidence(string[] rawLines, int startIndex, int endIndex)
    {
        var builder = new StringBuilder();
        for (var i = startIndex; i <= endIndex; i++)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(rawLines[i].Trim());
        }

        return builder.ToString();
    }

    private static int FindStructDeclarationNearAttribute(string[] codeLines, int attributeIndex)
    {
        var limit = Math.Min(codeLines.Length - 1, attributeIndex + 24);
        for (var i = attributeIndex; i <= limit; i++)
        {
            if (StructDeclarationRegex.IsMatch(codeLines[i]))
            {
                return i;
            }

            if (i != attributeIndex &&
                (codeLines[i].Contains("[StructLayout", StringComparison.Ordinal) ||
                 Regex.IsMatch(codeLines[i], @"^\s*(?:public|internal|private|protected)?\s*(?:class|interface|enum)\s+")))
            {
                return -1;
            }
        }

        return -1;
    }

    private static bool StructImplementsISignal(string[] codeLines, int index)
    {
        var builder = new StringBuilder();
        var limit = Math.Min(codeLines.Length - 1, index + 3);
        for (var i = index; i <= limit; i++)
        {
            builder.Append(' ');
            builder.Append(codeLines[i]);
            if (codeLines[i].Contains('{'))
            {
                break;
            }
        }

        var header = builder.ToString();
        var braceIndex = header.IndexOf('{', StringComparison.Ordinal);
        if (braceIndex >= 0)
        {
            header = header[..braceIndex];
        }

        var whereMatch = Regex.Match(header, @"\bwhere\b");
        if (whereMatch.Success)
        {
            header = header[..whereMatch.Index];
        }

        return Regex.IsMatch(header, @":\s*[^{};]*\bISignal\b");
    }

    private static bool StructImplementsBurstJob(string[] codeLines, int index)
    {
        var builder = new StringBuilder();
        var limit = Math.Min(codeLines.Length - 1, index + 3);
        for (var i = index; i <= limit; i++)
        {
            builder.Append(' ');
            builder.Append(codeLines[i]);
            if (codeLines[i].Contains('{'))
            {
                break;
            }
        }

        var header = builder.ToString();
        var braceIndex = header.IndexOf('{', StringComparison.Ordinal);
        if (braceIndex >= 0)
        {
            header = header[..braceIndex];
        }

        var whereMatch = Regex.Match(header, @"\bwhere\b");
        if (whereMatch.Success)
        {
            header = header[..whereMatch.Index];
        }

        return Regex.IsMatch(header, @":\s*[^{};]*\bIJob(?:ParallelFor|For|Entity|Chunk)?\b");
    }

    private static bool StructBodyContainsExecuteMethod(string[] codeLines, int index)
    {
        var limit = Math.Min(codeLines.Length - 1, index + 240);
        var started = false;
        var depth = 0;
        for (var i = index; i <= limit; i++)
        {
            var code = codeLines[i];
            if (i > index && !started && StructDeclarationRegex.IsMatch(code))
            {
                return false;
            }

            if (code.Contains('{', StringComparison.Ordinal))
            {
                started = true;
            }

            if (started && Regex.IsMatch(code, @"\bvoid\s+Execute\s*\("))
            {
                return true;
            }

            if (started)
            {
                depth += CountBraceDelta(code);
                if (depth <= 0 && i > index)
                {
                    return false;
                }
            }
        }

        return false;
    }

    private static StructMetadata? FindNearestStructMetadata(List<StructMetadata> structs, int index)
    {
        StructMetadata? bestForward = null;
        for (var i = 0; i < structs.Count; i++)
        {
            var item = structs[i];
            if (item.Index < index || item.Index > index + 24)
            {
                continue;
            }

            if (bestForward is null || item.Index < bestForward.Index)
            {
                bestForward = item;
            }
        }

        if (bestForward is not null)
        {
            return bestForward;
        }

        StructMetadata? bestBack = null;
        for (var i = 0; i < structs.Count; i++)
        {
            var item = structs[i];
            if (item.Index > index || item.Index < index - 8)
            {
                continue;
            }

            if (bestBack is null || item.Index > bestBack.Index)
            {
                bestBack = item;
            }
        }

        return bestBack;
    }

    private static bool StructBodyContainsWideField(string[] codeLines, int index)
    {
        var limit = Math.Min(codeLines.Length - 1, index + 100);
        for (var i = index; i <= limit; i++)
        {
            if (i > index && codeLines[i].Contains("[StructLayout", StringComparison.Ordinal))
            {
                return false;
            }

            if (i > index && StructDeclarationRegex.IsMatch(codeLines[i]))
            {
                return false;
            }

            if (Regex.IsMatch(codeLines[i], @"\b(double|double2|double3|double4|long|ulong|IntPtr|UIntPtr)\b"))
            {
                return true;
            }
        }

        return false;
    }

    private static SignalBridgeContractInfo ClassifySignalQueueBridgeContract(string rawText, string declarationLine, string fieldName)
    {
        var hasOwner = ContainsAnyIgnoreCase(declarationLine, "owner:") ||
            ContainsAnyIgnoreCase(rawText, "owner:");
        var hasDrainPhase = ContainsAnyIgnoreCase(rawText, "drain phase", "drained", "flush", "flushed", "SystemDispatcher", "LateUpdate", "POST_SIMULATION", "VISUAL_SYNC");
        var hasFrameBudget = ContainsAnyIgnoreCase(rawText, "max frame budget", "max events", "capacity", "budget", "frame limit") ||
            Regex.IsMatch(rawText, @"\b" + Regex.Escape(fieldName) + @"\b[^\r\n]*(\[[0-9]+\]|capacity|budget)", RegexOptions.IgnoreCase);
        var hasOverflowPolicy = ContainsAnyIgnoreCase(rawText, "overflow", "drop newest", "drop oldest", "coalesce", "fail-fast", "next-frame", "prevents same-frame");
        var hasTelemetryCounter = ContainsAnyIgnoreCase(rawText, "telemetry", "counter", "dropped count", "dropped", "backpressure");
        return new SignalBridgeContractInfo(hasOwner, hasDrainPhase, hasFrameBudget, hasOverflowPolicy, hasTelemetryCounter);
    }

    private static OwnershipInfo GetOwnership(string relativePath, string rawText, string declarationLine, string fieldName, string collectionKind)
    {
        var escaped = BuildNativeFieldAliasPattern(rawText, fieldName, collectionKind);
        var escapedPrimary = Regex.Escape(fieldName);
        var fieldAccess = @"(?:[A-Za-z_][A-Za-z0-9_]*\.)*" + escaped;
        var registerToken = "RegisterNative" + collectionKind;
        var unregisterToken = "UnregisterNative" + collectionKind;
        var hasDirectNativeArrayAllocation = collectionKind == "Array" &&
            Regex.IsMatch(rawText, escaped + @"\s*=\s*new\s+NativeArray\s*<", RegexOptions.Singleline);
        var hasDirectNativeQueueAllocation = collectionKind == "Queue" &&
            Regex.IsMatch(rawText, escaped + @"\s*=\s*new\s+NativeQueue\s*<", RegexOptions.Singleline);
        var hasH8MemoryAllocate = collectionKind == "Array" &&
            Regex.IsMatch(rawText, escaped + @"\s*=\s*H8Memory\s*\.\s*Allocate\s*<", RegexOptions.Singleline);
        var hasH8MemoryRelease = collectionKind == "Array" &&
            Regex.IsMatch(rawText, @"H8Memory\s*\.\s*Release\s*\(\s*ref\s+" + fieldAccess + @"\b", RegexOptions.Singleline);
        var fieldAssignedByArrayAllocatorHelper = collectionKind == "Array" &&
            IsAssignedByArrayAllocatorHelper(rawText, escaped);
        var hasH8MemoryOwnership = false;
        var escapedHandle = Regex.Escape(fieldName + "Handle");
        var fieldAssignedFromResolvedAlias =
            Regex.IsMatch(rawText, @"\bref\s+" + escapedHandle + @"\b", RegexOptions.Singleline) &&
            Regex.IsMatch(rawText, escaped + @"\s*=\s*[A-Za-z_][A-Za-z0-9_]*\s*;", RegexOptions.Singleline);
        var helperVaultAllocatorAlias =
            fieldAssignedByArrayAllocatorHelper &&
            rawText.Contains("VaultGenerationHandle", StringComparison.Ordinal) &&
            rawText.Contains("EnsureGenerationHandle", StringComparison.Ordinal) &&
            rawText.Contains("TryResolveHandle", StringComparison.Ordinal) &&
            rawText.Contains("_vaultNativeStateMask", StringComparison.Ordinal);
        var hasVaultGenerationAlias =
            collectionKind == "Array" &&
            rawText.Contains("VaultGenerationHandle", StringComparison.Ordinal) &&
            ((rawText.Contains("ReleaseBuffer", StringComparison.Ordinal) &&
              (Regex.IsMatch(rawText, @"\bout\s+" + escaped + @"\b", RegexOptions.Singleline) || fieldAssignedFromResolvedAlias) &&
              Regex.IsMatch(rawText, @"\b(?:EnsureGenerationHandle|TryResolveHandle|TryEnsure[A-Za-z0-9_]*Buffer|TryEnsure[A-Za-z0-9_]*Array)\b", RegexOptions.Singleline)) ||
             helperVaultAllocatorAlias);
        var hasVaultAlias =
            declarationLine.Contains("Vault alias", StringComparison.OrdinalIgnoreCase) ||
            declarationLine.Contains("GlobalDataVault owns", StringComparison.OrdinalIgnoreCase) ||
            declarationLine.Contains("ResolveNativeBuffer", StringComparison.Ordinal) ||
            Regex.IsMatch(rawText, @"(?i)(Vault alias|GlobalDataVault owns|VaultBufferHandle)[^\r\n]*\b" + escaped + @"\b|\b" + escaped + @"\b[^\r\n]*(Vault alias|GlobalDataVault owns|VaultBufferHandle)") ||
            Regex.IsMatch(rawText, @"\bVaultBufferHandle\s*<[^>]+>\s+" + escapedPrimary + @"Handle\b") ||
            Regex.IsMatch(rawText, escaped + @"\s*=\s*[A-Za-z_][A-Za-z0-9_]*Handle\s*\.\s*Resolve\s*\(") ||
            Regex.IsMatch(rawText, escaped + @"\s*=\s*ResolveNativeBuffer\s*<") ||
            hasVaultGenerationAlias;

        var hasRegister = rawText.Contains(registerToken, StringComparison.Ordinal) &&
            Regex.IsMatch(rawText, registerToken + @"\s*\([^;]*(" + escaped + @"|nameof\s*\(\s*" + escaped + @"\s*\))", RegexOptions.Singleline);
        var hasUnregister = rawText.Contains(unregisterToken, StringComparison.Ordinal) &&
            Regex.IsMatch(rawText, unregisterToken + @"\s*\([^;]*(" + escaped + @"|nameof\s*\(\s*" + escaped + @"\s*\))", RegexOptions.Singleline);
        var hasDispose = rawText.Contains(".Dispose", StringComparison.Ordinal) &&
            Regex.IsMatch(rawText, escaped + @"\s*\.\s*Dispose\s*\(", RegexOptions.Singleline);
        var helperKindPattern = collectionKind == "Queue"
            ? @"(?:Array|Buffer|Native|Queue)"
            : @"(?:Array|Buffer|Native)";
        var helperLocalPattern = collectionKind == "Queue"
            ? @"(?:queue|nativeQueue|buffer|nativeArray|label)"
            : @"(?:array|buffer|nativeArray)";
        var fieldPassedToRegisterHelper = Regex.IsMatch(rawText, @"\b(?:Register|Track)[A-Za-z0-9_]*" + helperKindPattern + @"[A-Za-z0-9_]*\s*\(\s*(?:ref\s+)?" + fieldAccess + @"\b", RegexOptions.Singleline);
        var helperRegistersCollection = rawText.Contains(registerToken, StringComparison.Ordinal) &&
            Regex.IsMatch(rawText, registerToken + @"\s*\([^;]*" + helperLocalPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var fieldPassedToDisposeHelper = Regex.IsMatch(rawText, @"\b(?:Dispose|Release)[A-Za-z0-9_]*" + helperKindPattern + @"[A-Za-z0-9_]*\s*\(\s*(?:ref\s+)?" + fieldAccess + @"\b", RegexOptions.Singleline);
        var helperUnregistersCollection = rawText.Contains(unregisterToken, StringComparison.Ordinal) &&
            Regex.IsMatch(rawText, unregisterToken + @"\s*\([^;]*" + helperLocalPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var helperDisposesCollection = Regex.IsMatch(rawText, helperLocalPattern + @"\s*\.\s*Dispose\s*\(", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var helperReleasesH8MemoryCollection =
            collectionKind == "Array" &&
            fieldPassedToDisposeHelper &&
            rawText.Contains("H8Memory.Release", StringComparison.Ordinal) &&
            Regex.IsMatch(rawText, @"H8Memory\s*\.\s*Release\s*\(\s*ref\s+" + helperLocalPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
        hasH8MemoryOwnership = hasH8MemoryAllocate && (hasH8MemoryRelease || helperReleasesH8MemoryCollection);
        var helperAllocatesAndRegistersArray =
            collectionKind == "Array" &&
            fieldAssignedByArrayAllocatorHelper &&
            rawText.Contains(registerToken, StringComparison.Ordinal) &&
            rawText.Contains("new NativeArray", StringComparison.Ordinal);
        var helperDisposesRegisteredArray =
            collectionKind == "Array" &&
            fieldPassedToDisposeHelper &&
            rawText.Contains(unregisterToken, StringComparison.Ordinal) &&
            Regex.IsMatch(rawText, @"\bDispose[A-Za-z0-9_]*Array\s*<[^>]*>\s*\(\s*ref\s+NativeArray\s*<", RegexOptions.Singleline) &&
            Regex.IsMatch(rawText, @"\barray\s*\.\s*Dispose\s*\(", RegexOptions.Singleline);
        var hasHelperRegister = fieldPassedToRegisterHelper && helperRegistersCollection;
        if (helperAllocatesAndRegistersArray)
        {
            hasHelperRegister = true;
        }

        if (hasHelperRegister)
        {
            hasRegister = true;
        }

        var hasHelperDispose =
            (fieldPassedToDisposeHelper && helperUnregistersCollection && helperDisposesCollection) ||
            helperDisposesRegisteredArray;
        if (hasHelperDispose)
        {
            hasUnregister = true;
            hasDispose = true;
        }

        var isH8MemoryRootAllocator =
            collectionKind == "Array" &&
            relativePath.EndsWith("Assets/_Project/Scripts/Core/Memory/H8Memory.cs", StringComparison.Ordinal) &&
            hasDirectNativeArrayAllocation &&
            hasDispose;
        var hasAllocation = hasDirectNativeArrayAllocation ||
            hasDirectNativeQueueAllocation ||
            hasH8MemoryAllocate ||
            fieldAssignedByArrayAllocatorHelper ||
            hasVaultGenerationAlias ||
            Regex.IsMatch(rawText, escaped + @"\s*=\s*ResolveNativeBuffer\s*<", RegexOptions.Singleline) ||
            Regex.IsMatch(rawText, escaped + @"\s*=\s*[A-Za-z_][A-Za-z0-9_]*Handle\s*\.\s*Resolve\s*\(", RegexOptions.Singleline);

        return new OwnershipInfo(hasRegister, hasUnregister, hasDispose, hasVaultAlias, hasHelperDispose, hasH8MemoryOwnership, isH8MemoryRootAllocator, hasAllocation);
    }

    private static bool IsAssignedByArrayAllocatorHelper(string rawText, string escapedFieldName)
    {
        return Regex.IsMatch(
            rawText,
            escapedFieldName + @"\s*=\s*(?:[A-Za-z_][A-Za-z0-9_]*\.)?(?:Allocate[A-Za-z0-9_]*Array|[A-Za-z_][A-Za-z0-9_]*Allocate[A-Za-z0-9_]*Array)\s*<",
            RegexOptions.Singleline);
    }

    private static string BuildNativeFieldAliasPattern(string rawText, string fieldName, string collectionKind)
    {
        var aliases = new HashSet<string>(StringComparer.Ordinal) { fieldName };
        if (collectionKind == "Array")
        {
            var aliasRegex = new Regex(
                @"\bref\s+NativeArray\s*<[^>]+>\s+(?<alias>[A-Za-z_][A-Za-z0-9_]*)\s*=>\s*ref\s+[A-Za-z_][A-Za-z0-9_\.]*\." + Regex.Escape(fieldName) + @"\b",
                RegexOptions.Singleline);
            foreach (Match match in aliasRegex.Matches(rawText))
            {
                var alias = match.Groups["alias"].Value;
                if (!string.IsNullOrWhiteSpace(alias))
                {
                    aliases.Add(alias);
                }
            }
        }

        return "(?:" + string.Join("|", aliases.Select(Regex.Escape)) + ")";
    }

    private static ContainerTypes GetContainerTypes(string[] codeLines)
    {
        var signalBus = new HashSet<string>(StringComparer.Ordinal);
        var nativeQueue = new HashSet<string>(StringComparer.Ordinal);
        var nativeList = new HashSet<string>(StringComparer.Ordinal);
        var nativeArray = new HashSet<string>(StringComparer.Ordinal);

        foreach (var code in codeLines)
        {
            if (!code.Contains('<') || !ContainsAny(code, "SignalBus", "NativeQueue", "NativeList", "NativeArray"))
            {
                continue;
            }

            foreach (Match match in ContainerTypeRegex.Matches(code))
            {
                var type = match.Groups["type"].Value;
                switch (match.Groups["kind"].Value)
                {
                    case "SignalBus":
                        signalBus.Add(type);
                        break;
                    case "NativeQueue":
                        nativeQueue.Add(type);
                        break;
                    case "NativeList":
                        nativeList.Add(type);
                        break;
                    case "NativeArray":
                        nativeArray.Add(type);
                        break;
                }
            }
        }

        return new ContainerTypes(signalBus, nativeQueue, nativeList, nativeArray);
    }

    private static int CountBraceDelta(string line)
    {
        var delta = 0;
        foreach (var c in line)
        {
            if (c == '{')
            {
                delta++;
            }
            else if (c == '}')
            {
                delta--;
            }
        }

        return delta;
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        foreach (var value in values)
        {
            if (text.Contains(value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsAnyIgnoreCase(string text, params string[] values)
    {
        foreach (var value in values)
        {
            if (text.Contains(value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

internal sealed record CliOptions(string ProjectRoot, string OutputJson, string OutputMarkdown, string Scope, bool FailOnError, bool IncludeHotPathHeuristics, bool NoOutput, bool PrintFindings, int MaxConsoleFindings)
{
    public static CliOptions Parse(string[] args)
    {
        var projectRoot = Directory.GetCurrentDirectory();
        string? outputJson = null;
        string? outputMarkdown = null;
        var scope = "Full";
        var failOnError = false;
        var includeHotPathHeuristics = false;
        var noOutput = false;
        var printFindings = false;
        var maxConsoleFindings = 64;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--project-root":
                    projectRoot = RequireValue(args, ref i, arg);
                    break;
                case "--json":
                case "--output-json":
                    outputJson = RequireValue(args, ref i, arg);
                    break;
                case "--markdown":
                case "--output-markdown":
                    outputMarkdown = RequireValue(args, ref i, arg);
                    break;
                case "--scope":
                    scope = RequireValue(args, ref i, arg);
                    break;
                case "--fail-on-error":
                    failOnError = true;
                    break;
                case "--include-hot-path-heuristics":
                    includeHotPathHeuristics = true;
                    break;
                case "--no-output":
                case "--stdout-only":
                    noOutput = true;
                    break;
                case "--print-findings":
                    printFindings = true;
                    break;
                case "--max-findings":
                    if (!int.TryParse(RequireValue(args, ref i, arg), out maxConsoleFindings) || maxConsoleFindings < 1)
                        throw new InvalidOperationException("--max-findings must be a positive integer.");
                    break;
                case "--help":
                case "-h":
                    throw new InvalidOperationException("Usage: --project-root <path> --json <path> --markdown <path> [--scope Full|SignalCritical] [--include-hot-path-heuristics] [--fail-on-error] [--no-output] [--print-findings] [--max-findings <n>]");
                default:
                    throw new InvalidOperationException("Unknown argument: " + arg);
            }
        }

        projectRoot = Path.GetFullPath(projectRoot);
        if (!string.Equals(scope, "Full", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(scope, "SignalCritical", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid scope. Expected Full or SignalCritical.");
        }

        outputJson ??= Path.Combine(projectRoot, "Temp", "SignalBusContractAuditCli.json");
        outputMarkdown ??= Path.Combine(projectRoot, "Temp", "SignalBusContractAuditCli.md");
        outputJson = Path.GetFullPath(Path.IsPathRooted(outputJson) ? outputJson : Path.Combine(projectRoot, outputJson));
        outputMarkdown = Path.GetFullPath(Path.IsPathRooted(outputMarkdown) ? outputMarkdown : Path.Combine(projectRoot, outputMarkdown));

        return new CliOptions(projectRoot, outputJson, outputMarkdown, scope, failOnError, includeHotPathHeuristics, noOutput, printFindings, maxConsoleFindings);
    }

    private static string RequireValue(string[] args, ref int index, string name)
    {
        if (index + 1 >= args.Length)
        {
            throw new InvalidOperationException("Missing value for " + name);
        }

        index++;
        return args[index];
    }
}

internal static class MarkdownWriter
{
    public static string Write(AuditResult result)
    {
        var md = new StringBuilder();
        md.AppendLine("# SHINOBU_02 Signal Bus Contract Audit CLI");
        md.AppendLine();
        md.AppendLine("Evidence Class: " + result.EvidenceClass);
        md.AppendLine("Scope: " + result.Scope);
        md.AppendLine("Generated UTC: " + result.GeneratedUtc);
        md.AppendLine();
        md.AppendLine("## Summary");
        md.AppendLine();
        md.AppendLine("- Files scanned: " + result.ScannedFiles + " C# / " + result.ShaderFilesScanned + " compute");
        md.AppendLine("- Signal-like definitions found: " + result.SignalDefinitions);
        md.AppendLine("- Signal definitions still in Core/GlobalSignals.cs: " + result.CoreGlobalSignalDefinitions);
        md.AppendLine("- Pack=1 layouts: " + result.Pack1Layouts);
        md.AppendLine("- Runtime signal Pack=1 layouts: " + result.RuntimeSignalPack1Layouts);
        md.AppendLine("- Runtime signal transitive Pack=1 field hits: " + result.TransitivePack1FieldHits);
        md.AppendLine("- Signal-like definitions without nearby StructLayout: " + result.SignalsWithoutLayout);
        md.AppendLine("- Managed event surface hits: " + result.ManagedEventSurfaceHits);
        md.AppendLine("- Local native telemetry ring hits: " + result.LocalNativeTelemetryRings);
        md.AppendLine("- Registered local telemetry rings: " + result.RegisteredLocalTelemetryRings);
        md.AppendLine("- Local native signal queue hits: " + result.LocalNativeSignalQueues);
        md.AppendLine("- Compute 1024-thread-group hits: " + result.ComputeThreadGroupRiskHits);
        md.AppendLine("- Hot-path heuristic hits: " + result.HotPathRiskHits);
        md.AppendLine("- Cold/fatal sync I/O review hits: " + result.ColdSyncIoReviewHits);
        md.AppendLine("- Assembly contract boundary hits: " + result.AsmdefContractBoundaryHits);
        md.AppendLine("- Cache-line-critical stride debt hits: " + result.CacheLineCriticalStrideDebtHits);
        md.AppendLine("- Errors: " + result.Errors);
        md.AppendLine("- Warnings: " + result.Warnings);
        md.AppendLine("- Infos: " + result.Infos);
        md.AppendLine("- Confirmed/probable errors at confidence >= 90: " + result.ConfirmedErrors);
        md.AppendLine("- Review-only findings below confidence 75: " + result.ReviewOnlyFindings);
        md.AppendLine();
        md.AppendLine("## Rule Breakdown");
        md.AppendLine();
        foreach (var stat in result.RuleStats)
        {
            md.AppendLine("- " + stat.Rule + ": total " + stat.Count + ", errors " + stat.Errors + ", warnings " + stat.Warnings + ", infos " + stat.Infos + ", avg confidence " + stat.AverageConfidence);
        }

        md.AppendLine();
        md.AppendLine("## Classification Breakdown");
        md.AppendLine();
        foreach (var stat in result.ClassificationStats)
        {
            md.AppendLine("- " + stat.Classification + ": " + stat.Count);
        }

        md.AppendLine();
        md.AppendLine("## Findings");
        md.AppendLine();
        if (result.Findings.Length == 0)
        {
            md.AppendLine("No findings. This is static-source only, not runtime proof.");
        }
        else
        {
            foreach (var finding in result.Findings)
            {
                md.AppendLine("- [" + finding.Severity + "][" + finding.Confidence + "%][" + finding.Classification + "] " + finding.Rule + " | " + finding.Path + ":" + finding.Line + " | " + finding.Symbol);
                md.AppendLine("  Evidence kind: " + finding.EvidenceKind);
                md.AppendLine("  Evidence: `" + finding.Evidence.Replace("`", "'") + "`");
                md.AppendLine("  Required action: " + finding.RequiredAction);
            }
        }

        md.AppendLine();
        md.AppendLine("## Non-Claims");
        md.AppendLine();
        md.AppendLine("- This audit does not prove Unity import, player build, IL2CPP, runtime GC, profiler, scene wiring, or actual struct sizeof(T).");
        md.AppendLine("- Static confidence is not semantic proof. This CLI intentionally stays outside Unity and uses standard .NET only.");
        md.AppendLine("- This audit reports contract debt only. It does not modify runtime contracts.");
        return md.ToString();
    }
}

internal sealed record ContainerTypes(HashSet<string> SignalBus, HashSet<string> NativeQueue, HashSet<string> NativeList, HashSet<string> NativeArray);

internal sealed record PreprocessorFrame(string RootCondition, bool IsCurrentBranchEditorOnly);

internal sealed record StructMetadata(
    string Name,
    string Declaration,
    string Path,
    int Line,
    int Index,
    bool HasStructLayout,
    int LayoutSize,
    bool ImplementsISignal,
    bool ImplementsBurstJob,
    bool HasExecuteMethod,
    bool IsEditor,
    bool IsCoreGlobalSignals,
    bool IsCoreSignalFile,
    bool IsSignalLikeName);

internal sealed record ForwardStatement(string Text, int EndIndex);

internal sealed record SignalDefinition(string Name, string Path, int Line, bool HasStructLayout, bool ImplementsISignal, bool IsEditor, bool InCoreGlobalSignals, bool IsStrictRuntimeContract);

internal sealed record Pack1StructInfo(string Name, string Path, int Line, bool IsEditor, bool IsFileFormatLike, bool HasWideField);

internal sealed record StructLayoutInfo(string Name, string Path, int Line, bool HasStructLayout, int LayoutSize, bool IsEditor, bool IsCoreSignalFile);

internal sealed record AsmdefInfo(string Path, string RelativePath, string Name, HashSet<string> References, string Directory, int ReferenceLine);

internal sealed record SignalBridgeContractInfo(
    bool HasOwner,
    bool HasDrainPhase,
    bool HasFrameBudget,
    bool HasOverflowPolicy,
    bool HasTelemetryCounter)
{
    public bool IsComplete => HasOwner && HasDrainPhase && HasFrameBudget && HasOverflowPolicy && HasTelemetryCounter;

    public void ApplyTo(Dictionary<string, object?> tags)
    {
        tags["hasBridgeOwner"] = HasOwner;
        tags["hasBridgeDrainPhase"] = HasDrainPhase;
        tags["hasBridgeFrameBudget"] = HasFrameBudget;
        tags["hasBridgeOverflowPolicy"] = HasOverflowPolicy;
        tags["hasBridgeTelemetryCounter"] = HasTelemetryCounter;
        tags["hasCompleteBridgeContract"] = IsComplete;
    }
}

internal sealed record OwnershipInfo(
    bool HasRegister,
    bool HasUnregister,
    bool HasDispose,
    bool HasVaultAlias,
    bool HasHelperDispose,
    bool HasH8MemoryOwnership,
    bool IsH8MemoryRootAllocator,
    bool HasAllocation)
{
    public bool IsOwned => (HasRegister && HasUnregister && HasDispose) || HasH8MemoryOwnership || IsH8MemoryRootAllocator;

    public Dictionary<string, object?> ToTags(bool isEditor)
    {
        return new Dictionary<string, object?>
        {
            ["hasSentinelRegistration"] = HasRegister,
            ["hasSentinelUnregister"] = HasUnregister,
            ["hasDisposePath"] = HasDispose,
            ["hasVaultAlias"] = HasVaultAlias,
            ["hasHelperDisposePath"] = HasHelperDispose,
            ["hasH8MemoryOwnership"] = HasH8MemoryOwnership,
            ["isH8MemoryRootAllocator"] = IsH8MemoryRootAllocator,
            ["hasAllocation"] = HasAllocation,
            ["isEditor"] = isEditor
        };
    }
}

internal sealed record Finding(
    string Severity,
    string Rule,
    int Confidence,
    string Classification,
    string EvidenceKind,
    string Path,
    int Line,
    string Symbol,
    string Evidence,
    string RequiredAction,
    Dictionary<string, object?> Tags);

internal sealed record RuleStat(string Rule, int Count, int Errors, int Warnings, int Infos, double AverageConfidence);

internal sealed record ClassificationStat(string Classification, int Count);

internal sealed record AuditResult(
    string Agent,
    string EvidenceClass,
    string Scope,
    string GeneratedUtc,
    string ProjectRoot,
    int ScannedFiles,
    int ShaderFilesScanned,
    int Pack1Layouts,
    int RuntimeSignalPack1Layouts,
    int TransitivePack1FieldHits,
    int ManagedEventSurfaceHits,
    int LocalNativeTelemetryRings,
    int RegisteredLocalTelemetryRings,
    int LocalNativeSignalQueues,
    int ComputeThreadGroupRiskHits,
    int HotPathRiskHits,
    int ColdSyncIoReviewHits,
    int AsmdefContractBoundaryHits,
    int CacheLineCriticalStrideDebtHits,
    int SignalDefinitions,
    int CoreGlobalSignalDefinitions,
    int SignalsWithoutLayout,
    int Errors,
    int Warnings,
    int Infos,
    int ConfirmedErrors,
    int ReviewOnlyFindings,
    RuleStat[] RuleStats,
    ClassificationStat[] ClassificationStats,
    Finding[] Findings);
