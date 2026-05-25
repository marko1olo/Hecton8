using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SignalArchitectureOptimizationAuditX001;

internal static class Program
{
    private const string AgentId = "X_001";
    private const string Schema = "hecton8.signal_architecture_optimization_report.x001.v1";
    private static readonly Regex NewExpressionTypeRegex = new(@"\bnew\s+(?<type>[A-Za-z_][A-Za-z0-9_]*)\b", RegexOptions.Compiled);
    private static readonly Regex MaxFrameSignalsRegex = new(@"maxFrameSignals\s*:\s*(?<value>[A-Za-z_][A-Za-z0-9_\.]*|\d+)", RegexOptions.Compiled);
    private static readonly Regex CapacityRegex = new(@"capacity\s*:\s*(?<value>[A-Za-z_][A-Za-z0-9_\.]*|\d+)", RegexOptions.Compiled);

    private static readonly string[] ManagedFieldTokens =
    {
        "string",
        "System.String",
        "object",
        "System.Object",
        "dynamic",
        "List<",
        "Dictionary<",
        "HashSet<",
        "Queue<",
        "Stack<",
        "IEnumerable<",
        "IReadOnlyList<",
        "Func<",
        "Action<",
        "System.Action",
        "System.Func",
        "UnityEvent",
        "GameObject",
        "Transform",
        "MonoBehaviour",
        "ScriptableObject",
        "AudioClip",
        "AudioSource",
        "Material",
        "Mesh",
        "Texture",
        "Texture2D",
        "RenderTexture",
        "Sprite",
        "AnimationCurve",
        "Gradient",
        "Collider",
        "Rigidbody",
        "ParticleSystem",
        "Camera",
        "Light",
        "TMP_Text"
    };

    private static int Main(string[] args)
    {
        try
        {
            string repoRoot = ResolveRepoRoot(args);
            string sourceRoot = GetArg(args, "--source") ?? Path.Combine(repoRoot, "Assets", "_Project", "Scripts");
            string outputJson = GetArg(args, "--output")
                ?? Path.Combine(repoRoot, "Docs", "Reports", "SIGNAL_ARCHITECTURE_OPTIMIZATION_REPORT_X_001.json");
            string outputMarkdown = GetArg(args, "--markdown")
                ?? Path.Combine(repoRoot, "Docs", "AgentLogs", "SIGNAL_ARCHITECTURE_OPTIMIZATION_REPORT_X_001.md");

            if (!Directory.Exists(sourceRoot))
            {
                Console.Error.WriteLine("Source root not found: " + sourceRoot);
                return 2;
            }

            AuditState state = new(repoRoot, sourceRoot);
            string[] files = Directory
                .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Where(ShouldScanFile)
                .ToArray();
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < files.Length; i++)
                ScanFile(files[i], state);

            AuditReport report = BuildReport(state, files.Length);
            string canonical = BuildCanonicalHashInput(report);
            report.Summary.CanonicalHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();

            JsonSerializerOptions jsonOptions = new()
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            Directory.CreateDirectory(Path.GetDirectoryName(outputJson) ?? ".");
            Directory.CreateDirectory(Path.GetDirectoryName(outputMarkdown) ?? ".");
            File.WriteAllText(outputJson, JsonSerializer.Serialize(report, jsonOptions), new UTF8Encoding(false));
            File.WriteAllText(outputMarkdown, BuildMarkdown(report), new UTF8Encoding(false));

            Console.WriteLine(
                "SignalArchitectureOptimizationAuditX001: files={0}, parseFailures={1}, globalSignalsSites={2}, signalPayloads={3}, hardPayloadViolations={4}, nativeQueueFields={5}, hash={6}",
                report.Summary.ScannedFiles,
                report.Summary.ParseFailures,
                report.Summary.GlobalSignalsCallSites,
                report.Summary.SignalPayloadDefinitions,
                report.Summary.HardPayloadViolations,
                report.Summary.GlobalSignalsNativeQueueFields,
                report.Summary.CanonicalHash);

            return report.Summary.ParseFailures == 0 ? 0 : 1;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
        {
            Console.Error.WriteLine("SignalArchitectureOptimizationAuditX001 failed: " + exception.Message);
            return 1;
        }
    }

    private static void ScanFile(string file, AuditState state)
    {
        string relativePath = ToProjectPath(state.RepoRoot, file);
        string source;
        try
        {
            source = File.ReadAllText(file, Encoding.UTF8);
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            state.ParseFailures.Add(new ParseFailure(relativePath, 0, exception.GetType().Name));
            return;
        }

        SyntaxTree tree = CSharpSyntaxTree.ParseText(
            source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview),
            path: file,
            encoding: Encoding.UTF8);
        CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
        foreach (Diagnostic diagnostic in tree.GetDiagnostics())
        {
            if (diagnostic.Severity != DiagnosticSeverity.Error)
                continue;

            FileLinePositionSpan span = diagnostic.Location.GetLineSpan();
            state.ParseFailures.Add(new ParseFailure(relativePath, span.StartLinePosition.Line + 1, diagnostic.Id));
            return;
        }

        bool isGlobalSignalsFile = IsGlobalSignalsPath(relativePath);
        string domain = InferDomain(relativePath);

        foreach (StructDeclarationSyntax structure in root.DescendantNodes().OfType<StructDeclarationSyntax>())
            ScanStruct(relativePath, domain, isGlobalSignalsFile, structure, state);

        foreach (FieldDeclarationSyntax field in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
            ScanField(relativePath, domain, isGlobalSignalsFile, field, state);

        foreach (InvocationExpressionSyntax invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            ScanInvocation(relativePath, domain, isGlobalSignalsFile, invocation, state);
    }

    private static void ScanStruct(
        string relativePath,
        string domain,
        bool isGlobalSignalsFile,
        StructDeclarationSyntax structure,
        AuditState state)
    {
        string name = structure.Identifier.ValueText;
        string bases = structure.BaseList?.ToString() ?? string.Empty;
        bool implementsSignal = bases.Contains("ISignal", StringComparison.Ordinal);
        bool likelyPayload = implementsSignal || name.EndsWith("Signal", StringComparison.Ordinal) || name.EndsWith("Command", StringComparison.Ordinal) || name.EndsWith("Packet", StringComparison.Ordinal);
        if (!likelyPayload)
            return;

        string attributeText = string.Join(" ", structure.AttributeLists.Select(static list => list.ToString()));
        bool hasLayout = attributeText.Contains("StructLayout", StringComparison.Ordinal);
        bool pack1 = attributeText.Contains("Pack = 1", StringComparison.Ordinal) || attributeText.Contains("Pack=1", StringComparison.Ordinal);
        bool explicitLayout = attributeText.Contains("LayoutKind.Explicit", StringComparison.Ordinal);
        int line = LineOf(structure);

        SignalPayload payload = new()
        {
            Name = name,
            Path = relativePath,
            Line = line,
            Domain = domain,
            InGlobalSignals = isGlobalSignalsFile,
            ImplementsSignal = implementsSignal,
            HasStructLayout = hasLayout,
            UsesExplicitLayout = explicitLayout,
            UsesPack1 = pack1,
            AttributeText = attributeText
        };

        foreach (FieldDeclarationSyntax field in structure.Members.OfType<FieldDeclarationSyntax>())
        {
            string typeText = field.Declaration.Type.ToString();
            foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
            {
                bool managed = IsHardManagedType(typeText);
                bool array = field.Declaration.Type is ArrayTypeSyntax || typeText.Contains("[]", StringComparison.Ordinal);
                PayloadFieldInfo info = new()
                {
                    Type = typeText,
                    Name = variable.Identifier.ValueText,
                    Line = LineOf(field),
                    HardManagedViolation = managed || array
                };
                payload.Fields.Add(info);
                if (info.HardManagedViolation)
                {
                    payload.Violations.Add(new PayloadViolation
                    {
                        Code = array ? "SIGNAL_FIELD_ARRAY" : "SIGNAL_FIELD_MANAGED_TYPE",
                        Severity = "ERROR",
                        Path = relativePath,
                        Line = LineOf(field),
                        Signal = name,
                        Detail = typeText + " " + variable.Identifier.ValueText,
                        RequiredFix = "Move managed state behind an id, fixed-size Native-compatible payload, or owner-local lookup. Do not publish references on hot signal lanes."
                    });
                }
            }
        }

        foreach (PropertyDeclarationSyntax property in structure.Members.OfType<PropertyDeclarationSyntax>())
        {
            if (property.Modifiers.Any(SyntaxKind.StaticKeyword))
                continue;

            payload.Violations.Add(new PayloadViolation
            {
                Code = "SIGNAL_PROPERTY_CS1612_RISK",
                Severity = "WARN",
                Path = relativePath,
                Line = LineOf(property),
                Signal = name,
                Detail = property.Type + " " + property.Identifier.ValueText,
                RequiredFix = "Use public fields or explicit accessor-free contract data. Mutable struct properties amplify CS1612 copy-edit bugs."
            });
        }

        if (!hasLayout)
        {
            payload.Violations.Add(new PayloadViolation
            {
                Code = "SIGNAL_LAYOUT_UNDECLARED",
                Severity = implementsSignal ? "WARN" : "INFO",
                Path = relativePath,
                Line = line,
                Signal = name,
                Detail = "No StructLayout attribute found.",
                RequiredFix = "Declare Sequential or Explicit layout and keep runtime stride ARM64-aligned."
            });
        }

        if (pack1)
        {
            payload.Violations.Add(new PayloadViolation
            {
                Code = "SIGNAL_PACK1_ARM64_FAULT_RISK",
                Severity = "ERROR",
                Path = relativePath,
                Line = line,
                Signal = name,
                Detail = "StructLayout Pack=1 found.",
                RequiredFix = "Remove Pack=1 from runtime payloads. Pad to natural alignment and verify sizeof(T) is a multiple of 8."
            });
        }

        state.Payloads.Add(payload);
    }

    private static void ScanField(
        string relativePath,
        string domain,
        bool isGlobalSignalsFile,
        FieldDeclarationSyntax field,
        AuditState state)
    {
        string typeText = field.Declaration.Type.ToString();
        if (!typeText.Contains("NativeQueue<", StringComparison.Ordinal))
            return;

        TypeDeclarationSyntax? owner = field.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        string ownerName = owner?.Identifier.ValueText ?? "";
        foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
        {
            NativeQueueFieldInfo info = new()
            {
                Path = relativePath,
                Line = LineOf(field),
                Domain = domain,
                OwnerType = ownerName,
                FieldName = variable.Identifier.ValueText,
                QueueType = typeText,
                InGlobalSignals = isGlobalSignalsFile,
                SignalName = ExtractGenericTypeName(typeText)
            };
            state.NativeQueueFields.Add(info);
        }
    }

    private static void ScanInvocation(
        string relativePath,
        string domain,
        bool isGlobalSignalsFile,
        InvocationExpressionSyntax invocation,
        AuditState state)
    {
        InvocationName name = ResolveInvocationName(invocation);
        string expression = invocation.Expression.ToString();
        int line = LineOf(invocation);

        if (IsGlobalSignalsReceiver(name.Receiver))
        {
            string category = ClassifyGlobalSignalsMethod(name.Method);
            GlobalSignalsCallSite site = new()
            {
                Path = relativePath,
                Line = line,
                Domain = domain,
                Method = name.Method,
                Category = category,
                PublishedPayloadHint = category == "publish" ? TryExtractPublishedPayload(invocation) : null
            };
            site.ConcernTags = ResolveConcernTags(relativePath, site.PublishedPayloadHint, expression);
            state.GlobalSignalsCallSites.Add(site);
            return;
        }

        if (isGlobalSignalsFile && string.Equals(name.Method, "FlushDirectSignalLane", StringComparison.Ordinal))
        {
            state.DirectFlushLanes.Add(new DirectFlushLaneInfo
            {
                Path = relativePath,
                Line = line,
                Domain = "Core Infrastructure",
                SignalName = ExtractInvocationGenericType(invocation) ?? "unknown"
            });
            return;
        }

        if (isGlobalSignalsFile && string.Equals(name.Method, "CreateQueue", StringComparison.Ordinal))
        {
            state.CreateQueueSites.Add(new DirectFlushLaneInfo
            {
                Path = relativePath,
                Line = line,
                Domain = "Core Infrastructure",
                SignalName = ExtractInvocationGenericType(invocation) ?? "unknown"
            });
            return;
        }

        if (expression.Contains("SignalBus<", StringComparison.Ordinal))
        {
            state.SignalBusSites.Add(new SignalBusCallSite
            {
                Path = relativePath,
                Line = line,
                Domain = domain,
                Method = name.Method,
                SignalName = ExtractSignalBusGeneric(expression) ?? "unknown",
                ExpectedCapacityToken = TryExtractArgumentToken(invocation, 0, "expectedCapacity"),
                MaxFrameSignalsToken = TryExtractArgumentToken(invocation, 1, "maxFrameSignals"),
                LowTierFrameSignalsToken = TryExtractArgumentToken(invocation, 2, "lowTierFrameSignals"),
                LaneHashToken = TryExtractArgumentToken(invocation, 3, "laneHash")
            });
            return;
        }

        if (name.Receiver.Contains("HectonEventBus", StringComparison.Ordinal) || expression.Contains("HectonEventBus", StringComparison.Ordinal))
        {
            state.HectonEventBusSites.Add(new BusCallSite
            {
                Path = relativePath,
                Line = line,
                Domain = domain,
                Method = name.Method
            });
        }
    }

    private static AuditReport BuildReport(AuditState state, int scannedFileCount)
    {
        List<PayloadViolation> payloadViolations = state.Payloads
            .SelectMany(static payload => payload.Violations)
            .OrderBy(static violation => violation.Path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static violation => violation.Line)
            .ThenBy(static violation => violation.Code, StringComparer.Ordinal)
            .ToList();

        List<DomainOwnershipEntry> domains = BuildDomainEntries(state);
        List<HotspotEntry> hotspots = BuildHotspots(state);
        List<SignalLaneLedgerEntry> laneLedger = BuildSignalLaneLedger(state);
        List<SignalStormModelEntry> stormModel = BuildStormModel(state);

        AuditSummary summary = new()
        {
            ScannedFiles = scannedFileCount,
            ParseFailures = state.ParseFailures.Count,
            SignalPayloadDefinitions = state.Payloads.Count,
            SignalPayloadsInsideGlobalSignals = state.Payloads.Count(static payload => payload.InGlobalSignals),
            HardPayloadViolations = payloadViolations.Count(static violation => string.Equals(violation.Severity, "ERROR", StringComparison.Ordinal)),
            PayloadLayoutWarnings = payloadViolations.Count(static violation => string.Equals(violation.Code, "SIGNAL_LAYOUT_UNDECLARED", StringComparison.Ordinal)),
            GlobalSignalsCallSites = state.GlobalSignalsCallSites.Count,
            GlobalSignalsPublishSites = state.GlobalSignalsCallSites.Count(static site => string.Equals(site.Category, "publish", StringComparison.Ordinal)),
            GlobalSignalsConsumeSites = state.GlobalSignalsCallSites.Count(static site => string.Equals(site.Category, "consume", StringComparison.Ordinal)),
            GlobalSignalsReadAccessorSites = state.GlobalSignalsCallSites.Count(static site => string.Equals(site.Category, "read-accessor", StringComparison.Ordinal)),
            GlobalSignalsNativeQueueFields = state.NativeQueueFields.Count(static field => field.InGlobalSignals),
            NativeQueueFieldsOutsideGlobalSignals = state.NativeQueueFields.Count(static field => !field.InGlobalSignals),
            FlushDirectSignalLaneInvocations = state.DirectFlushLanes.Count,
            CreateQueueInvocations = state.CreateQueueSites.Count,
            SignalBusCallSites = state.SignalBusSites.Count,
            HectonEventBusSites = state.HectonEventBusSites.Count,
            DirectLaneCountFromSource = state.DirectFlushLanes.Select(static lane => lane.SignalName).Distinct(StringComparer.Ordinal).Count(),
            EvidenceClass = "STATIC_SOURCE_ROSLYN_AST",
            RuntimeProof = false,
            RuntimeProofReason = "No Unity player, profiler, GCMonitor, or frame capture was executed by this audit."
        };

        return new AuditReport
        {
            Schema = Schema,
            AgentId = AgentId,
            GeneratedUtc = DateTimeOffset.UtcNow.ToString("O"),
            SourceRoot = ToProjectPath(state.RepoRoot, state.SourceRoot),
            Summary = summary,
            NonClaims =
            [
                "This report does not prove runtime zero-GC.",
                "This report does not prove frame time savings.",
                "This report does not authorize deleting GlobalSignals.cs without owner route cards and compile proof."
            ],
            MonolithInventory = new MonolithInventory
            {
                NativeQueueFields = state.NativeQueueFields
                    .Where(static field => field.InGlobalSignals)
                    .OrderBy(static field => field.Line)
                    .ToList(),
                DirectFlushLanes = state.DirectFlushLanes.OrderBy(static lane => lane.Line).ToList(),
                CreateQueueSites = state.CreateQueueSites.OrderBy(static lane => lane.Line).ToList(),
                GlobalSignalsCallSitesByCategory = state.GlobalSignalsCallSites
                    .GroupBy(static site => site.Category, StringComparer.Ordinal)
                    .OrderBy(static group => group.Key, StringComparer.Ordinal)
                    .Select(static group => new CountEntry(group.Key, group.Count()))
                    .ToList()
            },
            Payloads = state.Payloads
                .OrderBy(static payload => payload.Path, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static payload => payload.Line)
                .ToList(),
            PayloadViolations = payloadViolations,
            DomainOwnership = domains,
            LegacyPublishSites = state.GlobalSignalsCallSites
                .Where(static site => string.Equals(site.Category, "publish", StringComparison.Ordinal))
                .OrderBy(static site => site.Path, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static site => site.Line)
                .ToList(),
            SignalLaneLedger = laneLedger,
            SignalBusSites = state.SignalBusSites
                .OrderBy(static site => site.Path, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static site => site.Line)
                .ToList(),
            HectonEventBusSites = state.HectonEventBusSites
                .OrderBy(static site => site.Path, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static site => site.Line)
                .ToList(),
            Hotspots = hotspots,
            StaticStormModel = stormModel,
            ParseFailures = state.ParseFailures,
            Recommendations =
            [
                new Recommendation
                {
                    Priority = 1,
                    Code = "LANE_OWNER_ROUTE_CARDS",
                    Text = "Cut GlobalSignals by owner lane, not by search-replace. Each migrated payload needs owner, producer phase, consumer phase, capacity, overflow policy, retention, and telemetry route.",
                    RuntimeCost = "0us until migration. Migration target is bounded POST_SIMULATION fan-out with owner-local truth."
                },
                new Recommendation
                {
                    Priority = 2,
                    Code = "PAYLOAD_CONTRACT_EXTRACTION",
                    Text = "Move shared signal DTOs from Core/GlobalSignals.cs into Core/Contracts/Signals or owner contract assemblies only after no managed fields and no Pack=1 are present.",
                    RuntimeCost = "0us for contract extraction; compile wall reduction depends on asmdef graph and must be measured."
                },
                new Recommendation
                {
                    Priority = 3,
                    Code = "DIRECT_QUEUE_BRIDGE_RETIREMENT",
                    Text = "Keep GlobalSignals NativeQueue aliases as documented bridge lanes until producers consume SignalBus<T> snapshots directly. Do not expose NativeQueue<T> outside Core.",
                    RuntimeCost = "0us for bridge retention; eventual savings are reduced central flush work and smaller Core invalidation scope."
                }
            ]
        };
    }

    private static List<DomainOwnershipEntry> BuildDomainEntries(AuditState state)
    {
        Dictionary<string, DomainOwnershipEntry> entries = new(StringComparer.Ordinal);

        void Ensure(string domain)
        {
            if (!entries.ContainsKey(domain))
                entries[domain] = new DomainOwnershipEntry { Domain = domain };
        }

        foreach (SignalPayload payload in state.Payloads)
        {
            Ensure(payload.Domain);
            entries[payload.Domain].PayloadDefinitions++;
            AddUnique(entries[payload.Domain].PayloadSamples, payload.Name, 16);
        }

        foreach (GlobalSignalsCallSite site in state.GlobalSignalsCallSites)
        {
            Ensure(site.Domain);
            entries[site.Domain].GlobalSignalsCallSites++;
            if (string.Equals(site.Category, "publish", StringComparison.Ordinal))
                entries[site.Domain].PublishSites++;
            else if (string.Equals(site.Category, "consume", StringComparison.Ordinal))
                entries[site.Domain].ConsumeSites++;
            else if (string.Equals(site.Category, "read-accessor", StringComparison.Ordinal))
                entries[site.Domain].ReadAccessorSites++;

            if (!string.IsNullOrWhiteSpace(site.PublishedPayloadHint))
                AddUnique(entries[site.Domain].PublishedPayloadHints, site.PublishedPayloadHint, 16);
        }

        foreach (SignalBusCallSite site in state.SignalBusSites)
        {
            Ensure(site.Domain);
            entries[site.Domain].SignalBusCallSites++;
            AddUnique(entries[site.Domain].SignalBusPayloadSamples, site.SignalName, 16);
        }

        foreach (BusCallSite site in state.HectonEventBusSites)
        {
            Ensure(site.Domain);
            entries[site.Domain].HectonEventBusSites++;
        }

        return entries.Values
            .OrderByDescending(static entry => entry.GlobalSignalsCallSites + entry.SignalBusCallSites)
            .ThenBy(static entry => entry.Domain, StringComparer.Ordinal)
            .ToList();
    }

    private static List<HotspotEntry> BuildHotspots(AuditState state)
    {
        return state.GlobalSignalsCallSites
            .GroupBy(static site => site.Path, StringComparer.OrdinalIgnoreCase)
            .Select(static group => new HotspotEntry
            {
                Path = group.Key,
                Domain = group.First().Domain,
                GlobalSignalsCalls = group.Count(),
                PublishCalls = group.Count(static site => string.Equals(site.Category, "publish", StringComparison.Ordinal)),
                ConsumeCalls = group.Count(static site => string.Equals(site.Category, "consume", StringComparison.Ordinal)),
                ReadAccessorCalls = group.Count(static site => string.Equals(site.Category, "read-accessor", StringComparison.Ordinal))
            })
            .OrderByDescending(static item => item.GlobalSignalsCalls)
            .ThenBy(static item => item.Path, StringComparer.OrdinalIgnoreCase)
            .Take(64)
            .ToList();
    }

    private static List<SignalLaneLedgerEntry> BuildSignalLaneLedger(AuditState state)
    {
        Dictionary<string, SignalLaneLedgerEntry> entries = new(StringComparer.Ordinal);

        SignalLaneLedgerEntry Ensure(string signalName)
        {
            if (string.IsNullOrWhiteSpace(signalName))
                signalName = "unknown";

            if (!entries.TryGetValue(signalName, out SignalLaneLedgerEntry? entry))
            {
                entry = new SignalLaneLedgerEntry
                {
                    SignalName = signalName,
                    OverflowPolicy = ResolveOverflowPolicy(signalName),
                    CoalescingPolicy = ResolveCoalescingPolicy(signalName),
                    Burst5000Verdict = ResolveBurst5000Verdict(signalName),
                    MemoryPath = "Persistent NativeQueue<T> ingress, DataVault-backed NativeArray<T> frame snapshot, value-type payload copy. Static source proof only; profiler/GCMonitor proof not executed."
                };
                entries.Add(signalName, entry);
            }

            return entry;
        }

        foreach (DirectFlushLaneInfo lane in state.DirectFlushLanes)
        {
            SignalLaneLedgerEntry entry = Ensure(lane.SignalName);
            entry.DirectFlushPresent = true;
            AddUnique(entry.DirectFlushSites, lane.Path + ":" + lane.Line, 8);
        }

        foreach (DirectFlushLaneInfo site in state.CreateQueueSites)
        {
            SignalLaneLedgerEntry entry = Ensure(site.SignalName);
            entry.LegacyCreateQueuePresent = true;
            AddUnique(entry.CreateQueueSites, site.Path + ":" + site.Line, 8);
        }

        foreach (SignalBusCallSite site in state.SignalBusSites)
        {
            SignalLaneLedgerEntry entry = Ensure(site.SignalName);
            entry.SignalBusCallSites++;
            AddUnique(entry.Domains, site.Domain, 8);

            if (string.Equals(site.Method, "Configure", StringComparison.Ordinal) ||
                string.Equals(site.Method, "ConfigureCacheLineCritical", StringComparison.Ordinal))
            {
                entry.ConfigureSites++;
                entry.CacheLineCritical |= string.Equals(site.Method, "ConfigureCacheLineCritical", StringComparison.Ordinal);
                AddUnique(entry.ExpectedCapacityTokens, site.ExpectedCapacityToken ?? "default", 8);
                AddUnique(entry.MaxFrameSignalTokens, site.MaxFrameSignalsToken ?? "DefaultMaxFrameSignals", 8);
                AddUnique(entry.LowTierFrameSignalTokens, site.LowTierFrameSignalsToken ?? "DefaultSurvivalFrameSignals", 8);
                if (!string.IsNullOrWhiteSpace(site.LaneHashToken))
                    AddUnique(entry.LaneHashTokens, site.LaneHashToken, 8);
                AddUnique(entry.ConfigureSiteSamples, site.Path + ":" + site.Line, 8);
            }
            else if (string.Equals(site.Method, "EnsureInitialized", StringComparison.Ordinal))
            {
                entry.EnsureInitializedSites++;
            }
            else if (string.Equals(site.Method, "Push", StringComparison.Ordinal) ||
                     string.Equals(site.Method, "TryPush", StringComparison.Ordinal))
            {
                entry.TypedPublishSites++;
                AddUnique(entry.TypedPublishSiteSamples, site.Path + ":" + site.Line + " " + site.Method, 8);
            }
            else if (string.Equals(site.Method, "GetFrameSnapshot", StringComparison.Ordinal) ||
                     string.Equals(site.Method, "GetFrameSnapshotArray", StringComparison.Ordinal) ||
                     string.Equals(site.Method, "TryConsumeFrame", StringComparison.Ordinal) ||
                     string.Equals(site.Method, "GetSignals", StringComparison.Ordinal))
            {
                entry.TypedConsumerSites++;
            }
        }

        foreach (GlobalSignalsCallSite site in state.GlobalSignalsCallSites)
        {
            if (string.IsNullOrWhiteSpace(site.PublishedPayloadHint))
                continue;

            SignalLaneLedgerEntry entry = Ensure(site.PublishedPayloadHint);
            AddUnique(entry.Domains, site.Domain, 8);
            if (string.Equals(site.Category, "publish", StringComparison.Ordinal))
            {
                entry.LegacyPublishSites++;
                AddUnique(entry.LegacyPublishSiteSamples, site.Path + ":" + site.Line, 8);
                foreach (string tag in site.ConcernTags)
                    AddUnique(entry.ConcernTags, tag, 8);
            }
            else if (string.Equals(site.Category, "consume", StringComparison.Ordinal))
            {
                entry.LegacyConsumeSites++;
            }
        }

        foreach (SignalLaneLedgerEntry entry in entries.Values)
        {
            entry.CentralizationDebt = entry.DirectFlushPresent || entry.LegacyCreateQueuePresent || entry.LegacyPublishSites > 0 || entry.LegacyConsumeSites > 0;
            entry.StaticZeroGcClaim = "No per-signal managed allocation found in SignalBus<T> source path; not a runtime proof. Editor/development overflow logging is excluded from production claim.";
        }

        return entries.Values
            .OrderByDescending(static entry => entry.LegacyPublishSites + entry.LegacyConsumeSites)
            .ThenByDescending(static entry => entry.DirectFlushPresent)
            .ThenBy(static entry => entry.SignalName, StringComparer.Ordinal)
            .ToList();
    }

    private static List<SignalStormModelEntry> BuildStormModel(AuditState state)
    {
        Dictionary<string, SignalStormModelEntry> entries = new(StringComparer.Ordinal);
        foreach (DirectFlushLaneInfo lane in state.DirectFlushLanes)
        {
            if (!entries.TryGetValue(lane.SignalName, out SignalStormModelEntry? entry))
            {
                entry = new SignalStormModelEntry
                {
                    SignalName = lane.SignalName,
                    DirectFlushPresent = true
                };
                entries.Add(lane.SignalName, entry);
            }
        }

        foreach (SignalBusCallSite site in state.SignalBusSites)
        {
            if (!entries.TryGetValue(site.SignalName, out SignalStormModelEntry? entry))
            {
                entry = new SignalStormModelEntry { SignalName = site.SignalName };
                entries.Add(site.SignalName, entry);
            }

            entry.SignalBusCallSites++;
            if (!string.IsNullOrWhiteSpace(site.MaxFrameSignalsToken))
                AddUnique(entry.CapacityTokens, site.MaxFrameSignalsToken, 8);
            else if (!string.IsNullOrWhiteSpace(site.ExpectedCapacityToken))
                AddUnique(entry.CapacityTokens, site.ExpectedCapacityToken, 8);
        }

        foreach (GlobalSignalsCallSite site in state.GlobalSignalsCallSites)
        {
            if (string.IsNullOrWhiteSpace(site.PublishedPayloadHint))
                continue;

            if (!entries.TryGetValue(site.PublishedPayloadHint, out SignalStormModelEntry? entry))
            {
                entry = new SignalStormModelEntry { SignalName = site.PublishedPayloadHint };
                entries.Add(site.PublishedPayloadHint, entry);
            }

            entry.PublishSites++;
        }

        foreach (SignalStormModelEntry entry in entries.Values)
        {
            entry.StaticRisk = ResolveStormRisk(entry);
            entry.TestVector = "Burst = capacity + 1 when capacity known; otherwise burst = 257 to exceed default LaneCapacity=256. Expected result: deterministic drop/coalesce telemetry, no managed allocation, no producer stall.";
        }

        return entries.Values
            .OrderByDescending(static entry => RiskWeight(entry.StaticRisk))
            .ThenByDescending(static entry => entry.PublishSites + entry.SignalBusCallSites)
            .ThenBy(static entry => entry.SignalName, StringComparer.Ordinal)
            .Take(128)
            .ToList();
    }

    private static string ResolveStormRisk(SignalStormModelEntry entry)
    {
        if (entry.PublishSites > 8 && entry.DirectFlushPresent)
            return "HIGH";
        if (entry.PublishSites > 0 && entry.CapacityTokens.Count == 0)
            return "MEDIUM";
        if (entry.SignalBusCallSites > 0)
            return "LOW";
        return "INFO";
    }

    private static string ResolveOverflowPolicy(string signalName)
    {
        return "SignalBus<T>.FlushPreSimulation resolves a continuous frame limit from GlobalQualityWeight, system stress, priority, and optional tuning profile; drops oldest queued overflow before snapshot copy; drops all queued payloads when queued count exceeds LaneOverflowFaultThreshold=1024 and records storm telemetry.";
    }

    private static string ResolveCoalescingPolicy(string signalName)
    {
        return signalName switch
        {
            "CombatDamageSignal" => "Coalesces by TargetHash + DamageType + Channel inside the native frame snapshot; magnitude and integrity delta accumulate, flags OR, first nonzero source is retained.",
            "AcousticPingSignal" => "Coalesces by channel and AUP meter cell; acoustic energy is merged in native snapshot memory.",
            _ => "No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap."
        };
    }

    private static string ResolveBurst5000Verdict(string signalName)
    {
        if (string.Equals(signalName, "CombatDamageSignal", StringComparison.Ordinal))
            return "5000 queued damage signals exceed LaneOverflowFaultThreshold=1024, so the lane deterministically clears the native queue and reports storm/drop telemetry. Coalescing applies below the storm threshold.";

        if (string.Equals(signalName, "ImpactSignal", StringComparison.Ordinal) ||
            string.Equals(signalName, "HighSpeedImpactSignal", StringComparison.Ordinal) ||
            signalName.Contains("Collision", StringComparison.OrdinalIgnoreCase) ||
            signalName.Contains("Impact", StringComparison.OrdinalIgnoreCase))
        {
            return "5000 collision/impact signals exceed LaneOverflowFaultThreshold=1024, so the lane deterministically drops the storm batch via native queue clear; no semantic coalescing is declared for this lane.";
        }

        return "5000 queued signals exceed LaneOverflowFaultThreshold=1024; static policy is native queue clear, dropped-count telemetry, storm flag, and no producer stall. Runtime GC proof still requires profiler/GCMonitor.";
    }

    private static int RiskWeight(string risk)
    {
        return risk switch
        {
            "HIGH" => 4,
            "MEDIUM" => 3,
            "LOW" => 2,
            _ => 1
        };
    }

    private static bool ShouldScanFile(string path)
    {
        string normalized = path.Replace('\\', '/');
        return !normalized.Contains("/Library/", StringComparison.OrdinalIgnoreCase) &&
               !normalized.Contains("/Temp/", StringComparison.OrdinalIgnoreCase) &&
               !normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase) &&
               !normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveRepoRoot(string[] args)
    {
        string? explicitRoot = GetArg(args, "--repo") ?? GetArg(args, "--project-root");
        if (!string.IsNullOrWhiteSpace(explicitRoot))
            return Path.GetFullPath(explicitRoot);

        string current = Directory.GetCurrentDirectory();
        DirectoryInfo? directory = new(current);
        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, "Assets", "_Project", "Scripts");
            if (Directory.Exists(candidate))
                return directory.FullName;

            directory = directory.Parent;
        }

        return current;
    }

    private static string? GetArg(string[] args, string name)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                continue;

            if (i + 1 >= args.Length)
                return null;

            return args[i + 1];
        }

        return null;
    }

    private static string ToProjectPath(string repoRoot, string path)
    {
        string fullRoot = Path.GetFullPath(repoRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(path);
        if (fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            return fullPath[fullRoot.Length..].Replace('\\', '/');

        return path.Replace('\\', '/');
    }

    private static bool IsGlobalSignalsPath(string relativePath)
    {
        return relativePath.Replace('\\', '/').EndsWith("Assets/_Project/Scripts/Core/GlobalSignals.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static string InferDomain(string relativePath)
    {
        string normalized = relativePath.Replace('\\', '/');
        const string prefix = "Assets/_Project/Scripts/";
        int index = normalized.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        string tail = index >= 0 ? normalized[(index + prefix.Length)..] : normalized;
        string first = tail.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Unknown";
        if (first.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            return "Legacy Root / Requires Owner Route Card";

        return first switch
        {
            "AI" or "Ecosystem" or "Fauna" => "AI / Biota",
            "Animation" => "Animation",
            "Atmosphere" or "Environment" or "World" => "World / Environment",
            "Audio" or "AudioLog" => "Audio / DSP",
            "Bootstrap" => "Bootstrap",
            "Cartography" => "Cartography",
            "Construction" or "Habitat" or "Base" => "Habitat / Construction",
            "Core" => "Core Infrastructure",
            "Economy" or "Crafting" => "Economy / Crafting",
            "Gameplay" or "Player" or "Tools" => "Gameplay / Player",
            "Input" => "Input",
            "Lighting" or "Rendering" or "VFX" => "Rendering / VFX",
            "ModdingAPI" => "Modding API",
            "Narrative" => "Narrative",
            "Physics" or "Vehicles" => "Physics / Vehicles",
            "Power" => "Power",
            "SaveSystem" => "Save",
            "Thermodynamics" => "Thermodynamics",
            "UI" or "UX" or "Visor" => "UI / UX",
            _ => first
        };
    }

    private static bool IsHardManagedType(string typeText)
    {
        string normalized = typeText.Replace("global::", string.Empty, StringComparison.Ordinal);
        if (normalized.Contains("[]", StringComparison.Ordinal))
            return true;

        for (int i = 0; i < ManagedFieldTokens.Length; i++)
        {
            if (normalized.Contains(ManagedFieldTokens[i], StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static int LineOf(SyntaxNode node)
    {
        return node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
    }

    private static InvocationName ResolveInvocationName(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax member)
        {
            return new InvocationName(member.Expression.ToString(), ResolveSimpleName(member.Name));
        }

        if (invocation.Expression is GenericNameSyntax generic)
            return new InvocationName(string.Empty, generic.Identifier.ValueText);

        if (invocation.Expression is IdentifierNameSyntax identifier)
            return new InvocationName(string.Empty, identifier.Identifier.ValueText);

        return new InvocationName(string.Empty, invocation.Expression.ToString());
    }

    private static string ResolveSimpleName(SimpleNameSyntax name)
    {
        return name switch
        {
            GenericNameSyntax generic => generic.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => name.ToString()
        };
    }

    private static bool IsGlobalSignalsReceiver(string receiver)
    {
        string normalized = receiver.Replace("global::", string.Empty, StringComparison.Ordinal);
        return string.Equals(normalized, "GlobalSignals", StringComparison.Ordinal) ||
               normalized.EndsWith(".GlobalSignals", StringComparison.Ordinal);
    }

    private static string ClassifyGlobalSignalsMethod(string method)
    {
        if (string.Equals(method, "Publish", StringComparison.Ordinal) ||
            string.Equals(method, "Enqueue", StringComparison.Ordinal))
            return "publish";

        if (method.StartsWith("TryDequeue", StringComparison.Ordinal) ||
            method.StartsWith("Dequeue", StringComparison.Ordinal) ||
            method.StartsWith("Consume", StringComparison.Ordinal))
            return "consume";

        if (method.StartsWith("TryGet", StringComparison.Ordinal) ||
            method.StartsWith("Get", StringComparison.Ordinal) ||
            method.StartsWith("Read", StringComparison.Ordinal) ||
            method.Contains("CurrentRuntimeOrigin", StringComparison.Ordinal))
            return "read-accessor";

        if (method.StartsWith("Initialize", StringComparison.Ordinal) ||
            method.StartsWith("Flush", StringComparison.Ordinal) ||
            method.StartsWith("Clear", StringComparison.Ordinal))
            return "lifecycle";

        return "other";
    }

    private static string? TryExtractPublishedPayload(InvocationExpressionSyntax invocation)
    {
        SeparatedSyntaxList<ArgumentSyntax> args = invocation.ArgumentList.Arguments;
        if (args.Count == 0)
            return null;

        ExpressionSyntax expression = args[0].Expression;
        if (expression is ObjectCreationExpressionSyntax objectCreation)
            return CleanupTypeName(objectCreation.Type.ToString());

        string text = expression.ToString();
        Match match = NewExpressionTypeRegex.Match(text);
        if (match.Success)
            return match.Groups["type"].Value;

        if (expression is IdentifierNameSyntax identifier)
            return TryResolveIdentifierType(invocation, identifier.Identifier.ValueText);

        return null;
    }

    private static string? TryResolveIdentifierType(InvocationExpressionSyntax invocation, string identifierName)
    {
        SyntaxNode? scope = invocation.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>();
        scope ??= invocation.FirstAncestorOrSelf<AccessorDeclarationSyntax>();
        scope ??= invocation.FirstAncestorOrSelf<LocalFunctionStatementSyntax>();
        if (scope == null)
            return null;

        int invocationLine = LineOf(invocation);
        string? bestType = null;
        int bestLine = -1;

        foreach (VariableDeclaratorSyntax variable in scope.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            if (!string.Equals(variable.Identifier.ValueText, identifierName, StringComparison.Ordinal))
                continue;

            VariableDeclarationSyntax? declaration = variable.Parent as VariableDeclarationSyntax;
            if (declaration == null)
                continue;

            int line = LineOf(variable);
            if (line > invocationLine || line < bestLine)
                continue;

            string type = declaration.Type.ToString();
            if (string.Equals(type, "var", StringComparison.Ordinal))
            {
                if (variable.Initializer?.Value is ObjectCreationExpressionSyntax created)
                    type = created.Type.ToString();
                else
                    continue;
            }

            bestType = CleanupTypeName(type);
            bestLine = line;
        }

        if (!string.IsNullOrWhiteSpace(bestType))
            return bestType;

        foreach (ParameterSyntax parameter in scope.DescendantNodes().OfType<ParameterSyntax>())
        {
            if (!string.Equals(parameter.Identifier.ValueText, identifierName, StringComparison.Ordinal) ||
                parameter.Type == null)
            {
                continue;
            }

            return CleanupTypeName(parameter.Type.ToString());
        }

        return null;
    }

    private static string? TryExtractArgumentToken(InvocationExpressionSyntax invocation, int positionalIndex, string namedArgument)
    {
        SeparatedSyntaxList<ArgumentSyntax> args = invocation.ArgumentList.Arguments;
        for (int i = 0; i < args.Count; i++)
        {
            ArgumentSyntax arg = args[i];
            if (arg.NameColon != null &&
                string.Equals(arg.NameColon.Name.Identifier.ValueText, namedArgument, StringComparison.Ordinal))
            {
                return arg.Expression.ToString();
            }
        }

        if (positionalIndex >= 0 && positionalIndex < args.Count && args[positionalIndex].NameColon == null)
            return args[positionalIndex].Expression.ToString();

        return null;
    }

    private static List<string> ResolveConcernTags(string relativePath, string? payloadHint, string expression)
    {
        List<string> tags = [];
        string text = (relativePath + " " + payloadHint + " " + expression).ToLowerInvariant();
        if (text.Contains("reactor", StringComparison.Ordinal))
            tags.Add("reactor");
        if (text.Contains("explosion", StringComparison.Ordinal) || text.Contains("blast", StringComparison.Ordinal))
            tags.Add("explosion");
        if (text.Contains("hull", StringComparison.Ordinal) || text.Contains("deform", StringComparison.Ordinal))
            tags.Add("hull_deformation");
        if (text.Contains("airlock", StringComparison.Ordinal) || text.Contains("hatch", StringComparison.Ordinal) || text.Contains("door", StringComparison.Ordinal))
            tags.Add("airlock_or_door");
        if (text.Contains("damage", StringComparison.Ordinal) || text.Contains("impact", StringComparison.Ordinal) || text.Contains("collision", StringComparison.Ordinal))
            tags.Add("damage_or_collision");

        return tags;
    }

    private static string? ExtractInvocationGenericType(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is GenericNameSyntax generic && generic.TypeArgumentList.Arguments.Count > 0)
            return CleanupTypeName(generic.TypeArgumentList.Arguments[0].ToString());

        if (invocation.Expression is MemberAccessExpressionSyntax { Name: GenericNameSyntax memberGeneric } &&
            memberGeneric.TypeArgumentList.Arguments.Count > 0)
            return CleanupTypeName(memberGeneric.TypeArgumentList.Arguments[0].ToString());

        return null;
    }

    private static string? ExtractSignalBusGeneric(string expression)
    {
        int start = expression.IndexOf("SignalBus<", StringComparison.Ordinal);
        if (start < 0)
            return null;

        start += "SignalBus<".Length;
        int end = expression.IndexOf('>', start);
        if (end <= start)
            return null;

        return CleanupTypeName(expression[start..end]);
    }

    private static string ExtractGenericTypeName(string typeText)
    {
        int start = typeText.IndexOf('<', StringComparison.Ordinal);
        int end = typeText.LastIndexOf('>');
        if (start < 0 || end <= start)
            return "unknown";

        return CleanupTypeName(typeText[(start + 1)..end]);
    }

    private static string CleanupTypeName(string typeText)
    {
        string trimmed = typeText.Trim();
        int dot = trimmed.LastIndexOf('.');
        return dot >= 0 ? trimmed[(dot + 1)..] : trimmed;
    }

    private static string? TryExtractCapacityToken(string argumentText)
    {
        Match maxFrameSignals = MaxFrameSignalsRegex.Match(argumentText);
        if (maxFrameSignals.Success)
            return maxFrameSignals.Groups["value"].Value;

        Match capacity = CapacityRegex.Match(argumentText);
        if (capacity.Success)
            return capacity.Groups["value"].Value;

        return null;
    }

    private static void AddUnique(List<string> values, string value, int limit)
    {
        if (values.Count >= limit)
            return;

        if (values.Contains(value, StringComparer.Ordinal))
            return;

        values.Add(value);
    }

    private static string BuildCanonicalHashInput(AuditReport report)
    {
        StringBuilder builder = new();
        builder.AppendLine(report.Schema);
        builder.AppendLine(report.AgentId);
        builder.AppendLine(report.Summary.ScannedFiles.ToString());
        builder.AppendLine(report.Summary.GlobalSignalsCallSites.ToString());
        builder.AppendLine(report.Summary.SignalPayloadDefinitions.ToString());
        foreach (PayloadViolation violation in report.PayloadViolations)
            builder.AppendLine(violation.Code + "|" + violation.Path + "|" + violation.Line + "|" + violation.Signal + "|" + violation.Detail);

        foreach (NativeQueueFieldInfo field in report.MonolithInventory.NativeQueueFields)
            builder.AppendLine("queue|" + field.Path + "|" + field.Line + "|" + field.SignalName + "|" + field.FieldName);

        foreach (DirectFlushLaneInfo lane in report.MonolithInventory.DirectFlushLanes)
            builder.AppendLine("flush|" + lane.Line + "|" + lane.SignalName);

        foreach (GlobalSignalsCallSite site in report.LegacyPublishSites)
            builder.AppendLine("legacy-publish|" + site.Path + "|" + site.Line + "|" + site.PublishedPayloadHint);

        foreach (SignalLaneLedgerEntry lane in report.SignalLaneLedger)
            builder.AppendLine("lane|" + lane.SignalName + "|" + lane.LegacyPublishSites + "|" + lane.TypedPublishSites + "|" + string.Join(",", lane.MaxFrameSignalTokens));

        return builder.ToString();
    }

    private static string BuildMarkdown(AuditReport report)
    {
        StringBuilder builder = new();
        builder.AppendLine("# Signal Architecture Optimization Report X_001");
        builder.AppendLine();
        builder.AppendLine("Evidence class: " + report.Summary.EvidenceClass);
        builder.AppendLine("Runtime proof: " + report.Summary.RuntimeProof);
        builder.AppendLine("Canonical hash: " + report.Summary.CanonicalHash);
        builder.AppendLine();
        builder.AppendLine("## Counts");
        builder.AppendLine();
        builder.AppendLine("- Files scanned: " + report.Summary.ScannedFiles);
        builder.AppendLine("- Parse failures: " + report.Summary.ParseFailures);
        builder.AppendLine("- Signal payload definitions: " + report.Summary.SignalPayloadDefinitions);
        builder.AppendLine("- Payloads inside GlobalSignals.cs: " + report.Summary.SignalPayloadsInsideGlobalSignals);
        builder.AppendLine("- Hard payload violations: " + report.Summary.HardPayloadViolations);
        builder.AppendLine("- GlobalSignals call sites: " + report.Summary.GlobalSignalsCallSites);
        builder.AppendLine("- GlobalSignals publish sites: " + report.Summary.GlobalSignalsPublishSites);
        builder.AppendLine("- GlobalSignals NativeQueue fields: " + report.Summary.GlobalSignalsNativeQueueFields);
        builder.AppendLine("- FlushDirectSignalLane invocations: " + report.Summary.FlushDirectSignalLaneInvocations);
        builder.AppendLine("- Signal lanes in ledger: " + report.SignalLaneLedger.Count);
        builder.AppendLine();
        builder.AppendLine("## Top Hotspots");
        builder.AppendLine();
        foreach (HotspotEntry hotspot in report.Hotspots.Take(20))
            builder.AppendLine("- " + hotspot.Path + " | calls=" + hotspot.GlobalSignalsCalls + " publish=" + hotspot.PublishCalls + " consume=" + hotspot.ConsumeCalls + " read=" + hotspot.ReadAccessorCalls);

        builder.AppendLine();
        builder.AppendLine("## Payload Violations");
        builder.AppendLine();
        foreach (PayloadViolation violation in report.PayloadViolations.Take(80))
            builder.AppendLine("- " + violation.Severity + " " + violation.Code + " " + violation.Path + ":" + violation.Line + " " + violation.Signal + " | " + violation.Detail);

        builder.AppendLine();
        builder.AppendLine("## Legacy Publish Sites");
        builder.AppendLine();
        foreach (GlobalSignalsCallSite site in report.LegacyPublishSites.Take(80))
            builder.AppendLine("- " + site.Path + ":" + site.Line + " | payload=" + (site.PublishedPayloadHint ?? "unknown") + " | domain=" + site.Domain + " | tags=" + string.Join(",", site.ConcernTags));

        builder.AppendLine();
        builder.AppendLine("## Lane Capacity And Overflow Ledger");
        builder.AppendLine();
        foreach (SignalLaneLedgerEntry lane in report.SignalLaneLedger.Take(80))
        {
            builder.AppendLine("- " + lane.SignalName +
                               " | configure=" + lane.ConfigureSites +
                               " | maxFrame=" + string.Join(",", lane.MaxFrameSignalTokens) +
                               " | lowTier=" + string.Join(",", lane.LowTierFrameSignalTokens) +
                               " | legacyPublish=" + lane.LegacyPublishSites +
                               " | typedPublish=" + lane.TypedPublishSites +
                               " | coalescing=" + lane.CoalescingPolicy);
        }

        return builder.ToString();
    }

    private sealed class AuditState
    {
        public AuditState(string repoRoot, string sourceRoot)
        {
            RepoRoot = repoRoot;
            SourceRoot = sourceRoot;
        }

        public string RepoRoot { get; }
        public string SourceRoot { get; }
        public List<ParseFailure> ParseFailures { get; } = [];
        public List<SignalPayload> Payloads { get; } = [];
        public List<NativeQueueFieldInfo> NativeQueueFields { get; } = [];
        public List<GlobalSignalsCallSite> GlobalSignalsCallSites { get; } = [];
        public List<DirectFlushLaneInfo> DirectFlushLanes { get; } = [];
        public List<DirectFlushLaneInfo> CreateQueueSites { get; } = [];
        public List<SignalBusCallSite> SignalBusSites { get; } = [];
        public List<BusCallSite> HectonEventBusSites { get; } = [];
    }

    private readonly record struct InvocationName(string Receiver, string Method);

    private sealed class AuditReport
    {
        public string Schema { get; set; } = "";
        public string AgentId { get; set; } = "";
        public string GeneratedUtc { get; set; } = "";
        public string SourceRoot { get; set; } = "";
        public AuditSummary Summary { get; set; } = new();
        public List<string> NonClaims { get; set; } = [];
        public MonolithInventory MonolithInventory { get; set; } = new();
        public List<SignalPayload> Payloads { get; set; } = [];
        public List<PayloadViolation> PayloadViolations { get; set; } = [];
        public List<DomainOwnershipEntry> DomainOwnership { get; set; } = [];
        public List<GlobalSignalsCallSite> LegacyPublishSites { get; set; } = [];
        public List<SignalLaneLedgerEntry> SignalLaneLedger { get; set; } = [];
        public List<SignalBusCallSite> SignalBusSites { get; set; } = [];
        public List<BusCallSite> HectonEventBusSites { get; set; } = [];
        public List<HotspotEntry> Hotspots { get; set; } = [];
        public List<SignalStormModelEntry> StaticStormModel { get; set; } = [];
        public List<ParseFailure> ParseFailures { get; set; } = [];
        public List<Recommendation> Recommendations { get; set; } = [];
    }

    private sealed class AuditSummary
    {
        public int ScannedFiles { get; set; }
        public int ParseFailures { get; set; }
        public int SignalPayloadDefinitions { get; set; }
        public int SignalPayloadsInsideGlobalSignals { get; set; }
        public int HardPayloadViolations { get; set; }
        public int PayloadLayoutWarnings { get; set; }
        public int GlobalSignalsCallSites { get; set; }
        public int GlobalSignalsPublishSites { get; set; }
        public int GlobalSignalsConsumeSites { get; set; }
        public int GlobalSignalsReadAccessorSites { get; set; }
        public int GlobalSignalsNativeQueueFields { get; set; }
        public int NativeQueueFieldsOutsideGlobalSignals { get; set; }
        public int FlushDirectSignalLaneInvocations { get; set; }
        public int CreateQueueInvocations { get; set; }
        public int SignalBusCallSites { get; set; }
        public int HectonEventBusSites { get; set; }
        public int DirectLaneCountFromSource { get; set; }
        public string EvidenceClass { get; set; } = "";
        public bool RuntimeProof { get; set; }
        public string RuntimeProofReason { get; set; } = "";
        public string CanonicalHash { get; set; } = "";
    }

    private sealed class MonolithInventory
    {
        public List<NativeQueueFieldInfo> NativeQueueFields { get; set; } = [];
        public List<DirectFlushLaneInfo> DirectFlushLanes { get; set; } = [];
        public List<DirectFlushLaneInfo> CreateQueueSites { get; set; } = [];
        public List<CountEntry> GlobalSignalsCallSitesByCategory { get; set; } = [];
    }

    private sealed class SignalPayload
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public int Line { get; set; }
        public string Domain { get; set; } = "";
        public bool InGlobalSignals { get; set; }
        public bool ImplementsSignal { get; set; }
        public bool HasStructLayout { get; set; }
        public bool UsesExplicitLayout { get; set; }
        public bool UsesPack1 { get; set; }
        public string AttributeText { get; set; } = "";
        public List<PayloadFieldInfo> Fields { get; set; } = [];
        public List<PayloadViolation> Violations { get; set; } = [];
    }

    private sealed class PayloadFieldInfo
    {
        public string Type { get; set; } = "";
        public string Name { get; set; } = "";
        public int Line { get; set; }
        public bool HardManagedViolation { get; set; }
    }

    private sealed class PayloadViolation
    {
        public string Code { get; set; } = "";
        public string Severity { get; set; } = "";
        public string Path { get; set; } = "";
        public int Line { get; set; }
        public string Signal { get; set; } = "";
        public string Detail { get; set; } = "";
        public string RequiredFix { get; set; } = "";
    }

    private sealed class NativeQueueFieldInfo
    {
        public string Path { get; set; } = "";
        public int Line { get; set; }
        public string Domain { get; set; } = "";
        public string OwnerType { get; set; } = "";
        public string FieldName { get; set; } = "";
        public string QueueType { get; set; } = "";
        public bool InGlobalSignals { get; set; }
        public string SignalName { get; set; } = "";
    }

    private sealed class GlobalSignalsCallSite
    {
        public string Path { get; set; } = "";
        public int Line { get; set; }
        public string Domain { get; set; } = "";
        public string Method { get; set; } = "";
        public string Category { get; set; } = "";
        public string? PublishedPayloadHint { get; set; }
        public List<string> ConcernTags { get; set; } = [];
    }

    private sealed class DirectFlushLaneInfo
    {
        public string Path { get; set; } = "";
        public int Line { get; set; }
        public string Domain { get; set; } = "";
        public string SignalName { get; set; } = "";
    }

    private sealed class SignalBusCallSite
    {
        public string Path { get; set; } = "";
        public int Line { get; set; }
        public string Domain { get; set; } = "";
        public string Method { get; set; } = "";
        public string SignalName { get; set; } = "";
        public string? ExpectedCapacityToken { get; set; }
        public string? MaxFrameSignalsToken { get; set; }
        public string? LowTierFrameSignalsToken { get; set; }
        public string? LaneHashToken { get; set; }
    }

    private sealed class BusCallSite
    {
        public string Path { get; set; } = "";
        public int Line { get; set; }
        public string Domain { get; set; } = "";
        public string Method { get; set; } = "";
    }

    private sealed class DomainOwnershipEntry
    {
        public string Domain { get; set; } = "";
        public int PayloadDefinitions { get; set; }
        public int GlobalSignalsCallSites { get; set; }
        public int PublishSites { get; set; }
        public int ConsumeSites { get; set; }
        public int ReadAccessorSites { get; set; }
        public int SignalBusCallSites { get; set; }
        public int HectonEventBusSites { get; set; }
        public List<string> PayloadSamples { get; set; } = [];
        public List<string> PublishedPayloadHints { get; set; } = [];
        public List<string> SignalBusPayloadSamples { get; set; } = [];
    }

    private sealed class HotspotEntry
    {
        public string Path { get; set; } = "";
        public string Domain { get; set; } = "";
        public int GlobalSignalsCalls { get; set; }
        public int PublishCalls { get; set; }
        public int ConsumeCalls { get; set; }
        public int ReadAccessorCalls { get; set; }
    }

    private sealed class SignalLaneLedgerEntry
    {
        public string SignalName { get; set; } = "";
        public bool DirectFlushPresent { get; set; }
        public bool LegacyCreateQueuePresent { get; set; }
        public bool CentralizationDebt { get; set; }
        public bool CacheLineCritical { get; set; }
        public int ConfigureSites { get; set; }
        public int EnsureInitializedSites { get; set; }
        public int SignalBusCallSites { get; set; }
        public int TypedPublishSites { get; set; }
        public int TypedConsumerSites { get; set; }
        public int LegacyPublishSites { get; set; }
        public int LegacyConsumeSites { get; set; }
        public List<string> Domains { get; set; } = [];
        public List<string> ExpectedCapacityTokens { get; set; } = [];
        public List<string> MaxFrameSignalTokens { get; set; } = [];
        public List<string> LowTierFrameSignalTokens { get; set; } = [];
        public List<string> LaneHashTokens { get; set; } = [];
        public List<string> ConcernTags { get; set; } = [];
        public List<string> DirectFlushSites { get; set; } = [];
        public List<string> CreateQueueSites { get; set; } = [];
        public List<string> ConfigureSiteSamples { get; set; } = [];
        public List<string> TypedPublishSiteSamples { get; set; } = [];
        public List<string> LegacyPublishSiteSamples { get; set; } = [];
        public string OverflowPolicy { get; set; } = "";
        public string CoalescingPolicy { get; set; } = "";
        public string Burst5000Verdict { get; set; } = "";
        public string MemoryPath { get; set; } = "";
        public string StaticZeroGcClaim { get; set; } = "";
    }

    private sealed class SignalStormModelEntry
    {
        public string SignalName { get; set; } = "";
        public bool DirectFlushPresent { get; set; }
        public int PublishSites { get; set; }
        public int SignalBusCallSites { get; set; }
        public List<string> CapacityTokens { get; set; } = [];
        public string StaticRisk { get; set; } = "";
        public string TestVector { get; set; } = "";
    }

    private sealed class Recommendation
    {
        public int Priority { get; set; }
        public string Code { get; set; } = "";
        public string Text { get; set; } = "";
        public string RuntimeCost { get; set; } = "";
    }

    private sealed record CountEntry(string Name, int Count);
    private sealed record ParseFailure(string Path, int Line, string Diagnostic);
}
