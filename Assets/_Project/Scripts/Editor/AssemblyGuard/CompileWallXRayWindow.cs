using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEngine;

namespace Hecton8.Editor
{
    internal enum CompileWallAssemblyLayer : byte
    {
        Unknown = 0,
        Contracts = 1,
        Runtime = 2,
        Authoring = 3,
        Editor = 4,
        Tests = 5
    }

    internal sealed class CompileWallAsmdefNode
    {
        public string Name;
        public string DisplayName;
        public string Path;
        public string Guid;
        public string[] References;
        public string[] IncludePlatforms;
        public CompileWallAssemblyLayer Layer;
    }

    internal sealed class CompileWallAsmdefEdge
    {
        public CompileWallAsmdefNode From;
        public CompileWallAsmdefNode To;
        public bool IllegalRuntimeEdge;
    }

    internal sealed class CompileWallGraphScan
    {
        public const int MaxStoredPackViolations = 512;
        public const int MaxStoredCycleAssemblies = 128;
        public const int MaxStoredDomainHealthRows = 128;

        public readonly List<CompileWallAsmdefNode> Nodes = new List<CompileWallAsmdefNode>(128);
        public readonly List<CompileWallAsmdefEdge> Edges = new List<CompileWallAsmdefEdge>(256);
        public readonly List<CompileWallAsmdefEdge> IllegalRuntimeEdges = new List<CompileWallAsmdefEdge>(128);
        public readonly List<string> CycleAssemblies = new List<string>(MaxStoredCycleAssemblies);
        public readonly List<CompileWallDomainHealth> DomainHealth =
            new List<CompileWallDomainHealth>(MaxStoredDomainHealthRows);
        public readonly List<CompileWallArtifactSkew> ArtifactSkews = new List<CompileWallArtifactSkew>(128);
        public readonly List<CompileWallPackViolation> PackViolations = new List<CompileWallPackViolation>(MaxStoredPackViolations);
        public readonly List<string> ArchaeologyFiles = new List<string>(16);
        public readonly List<CompileWallLegacyGraphHeader> LegacyGraphHeaders = new List<CompileWallLegacyGraphHeader>(16);
        public readonly List<string> MockAtlasAssemblies = new List<string>(16);
        public int PackViolationTotal;
        public bool UsedEmergencyMockAtlas;
        public double ScanMilliseconds;

        public CompileWallAsmdefNode FindNodeByName(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
                return null;

            for (int i = 0; i < Nodes.Count; i++)
            {
                CompileWallAsmdefNode node = Nodes[i];
                if (node != null && string.Equals(node.Name, assemblyName, StringComparison.Ordinal))
                    return node;
            }

            return null;
        }
    }

    internal sealed class CompileWallDomainHealth
    {
        public string DomainName;
        public int AssemblyCount;
        public int RuntimeAssemblyCount;
        public int IllegalRuntimeEdges;
        public int CycleAssemblies;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32, Pack = 8)]
    internal struct CompileWallLegacyGraphHeader
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public ushort Version;
        [FieldOffset(6)] public ushort HeaderBytes;
        [FieldOffset(8)] public uint NodeCount;
        [FieldOffset(12)] public uint EdgeCount;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public uint PayloadBytes;
        [FieldOffset(24)] public uint Crc32;
        [FieldOffset(28)] public uint Reserved;
    }

    internal sealed class CompileWallArtifactSkew
    {
        public CompileWallAsmdefNode From;
        public CompileWallAsmdefNode To;
        public string FromRefPath;
        public string ToRefPath;
        public double SecondsBehind;
    }

    internal struct CompileWallRefArtifactRecord
    {
        public string Path;
        public long WriteTimeUtcTicks;
    }

    internal struct CompileWallAsmdefData
    {
        public string Name;
        public string[] References;
        public string[] IncludePlatforms;
    }

    internal sealed class CompileWallPackViolation
    {
        public string Path;
        public int LineNumber;
        public string Snippet;
    }

    internal static class CompileWallAssemblyGraphScanner
    {
        private const string ProjectAsmdefRoot = "Assets/_Project";
        private const string ArchiveRoot = "Docs/Archive";
        private const string StreamingAssetsRoot = "Assets/StreamingAssets";
        private const int LegacyGraphHeaderBytes = 32;
        private const int SourceScanBufferBytes = 8192;
        private const int AsmdefJsonBytes = 65536;
        private const int MaxAsmdefArrayItems = 128;
        private const int TypeSourceLineBytes = 2048;
        private const int TypeIdentifierChars = 256;
        private const uint LegacyGraphMagic = 0x48384147u;
        private const uint LegacyGraphHeaderFlagBigEndian = 0x80000000u;
        private const uint LegacyGraphHeaderFlagRecognizedMagic = 0x40000000u;
        private const string StructLayoutToken = "StructLayout";
        private const string PackToken = "Pack";
        private const string GuidToken = "guid:";
        private const string GuidReferencePrefix = "GUID:";
        private const string NameJsonProperty = "\"name\"";
        private const string ReferencesJsonProperty = "\"references\"";
        private const string IncludePlatformsJsonProperty = "\"includePlatforms\"";
        private const string EditorPathToken = "/Editor/";
        private const string EditorBackslashPathToken = "\\Editor\\";
        private const string TestsPathToken = "/Tests/";
        private const string TestsBackslashPathToken = "\\Tests\\";
        private const string ContractsPathToken = "/Contracts/";
        private const string ContractsBackslashPathToken = "\\Contracts\\";
        private const string AuthoringPathToken = "/Authoring/";
        private const string AuthoringBackslashPathToken = "\\Authoring\\";
        private const string NamespaceToken = "namespace ";
        private const string PublicToken = "public ";
        private const string ReadonlyToken = "readonly ";
        private const string StaticToken = "static ";
        private const string InterfaceToken = "interface ";
        private const string StructToken = "struct ";
        private const string EnumToken = "enum ";
        private const string DelegateToken = "delegate ";
        private const string ClassToken = "class ";
        private const int UnityGuidHexLength = 32;
        private const string PackOneSnippet = "forbidden runtime struct pack-one layout";
        private const string LegacyGraphFilePrefix = "asmdef_graph_";
        private const string LegacyGraphFileSuffix = ".h8bin";
        private const string ProjectAtlasFileName = "project_atlas.json";
        private const string RefDllSuffix = ".ref.dll";
        private static readonly byte[] LegacyHeaderScratch = new byte[LegacyGraphHeaderBytes];
        private static readonly byte[] SourceScanScratch = new byte[SourceScanBufferBytes];
        private static readonly byte[] AsmdefJsonScratch = new byte[AsmdefJsonBytes];
        private static readonly byte[] TypeLineScratch = new byte[TypeSourceLineBytes];
        private static readonly char[] GuidScratch = new char[UnityGuidHexLength];
        private static readonly char[] IdentifierScratch = new char[TypeIdentifierChars];
        private static readonly string PostProcessedPathToken =
            Path.DirectorySeparatorChar + "post-processed" + Path.DirectorySeparatorChar;

        public static CompileWallGraphScan ScanProject()
        {
            double started = EditorApplication.timeSinceStartup;
            var scan = new CompileWallGraphScan();
            string projectRoot = GetProjectRoot();
            string absoluteAsmdefRoot = Path.Combine(projectRoot, ProjectAsmdefRoot.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(absoluteAsmdefRoot))
            {
                scan.ScanMilliseconds = (EditorApplication.timeSinceStartup - started) * 1000.0;
                CompileWallBlackBox.RecordGraphScan(scan);
                return scan;
            }

            var byName = new Dictionary<string, CompileWallAsmdefNode>(128);
            var byGuidReference = new Dictionary<string, CompileWallAsmdefNode>(128);
            using (IEnumerator<string> files = Directory.EnumerateFiles(
                       absoluteAsmdefRoot,
                       "*.asmdef",
                       SearchOption.AllDirectories).GetEnumerator())
            {
                while (files.MoveNext())
                {
                    CompileWallAsmdefNode node = TryReadNode(projectRoot, files.Current);
                    if (node == null || string.IsNullOrEmpty(node.Name))
                        continue;

                    scan.Nodes.Add(node);
                    if (!byName.ContainsKey(node.Name))
                        byName.Add(node.Name, node);
                    if (!string.IsNullOrEmpty(node.Guid))
                    {
                        string guidReference = GuidReferencePrefix + node.Guid;
                        if (!byGuidReference.ContainsKey(guidReference))
                            byGuidReference.Add(guidReference, node);
                    }
                }
            }

            for (int i = 0; i < scan.Nodes.Count; i++)
            {
                CompileWallAsmdefNode node = scan.Nodes[i];
                string[] refs = node.References;
                if (refs == null)
                    continue;

                for (int r = 0; r < refs.Length; r++)
                {
                    string referenceName = ResolveReferenceName(refs[r], byGuidReference);
                    if (string.IsNullOrEmpty(referenceName))
                        continue;

                    if (!byName.TryGetValue(referenceName, out CompileWallAsmdefNode target))
                        continue;

                    var edge = new CompileWallAsmdefEdge
                    {
                        From = node,
                        To = target,
                        IllegalRuntimeEdge = node.Layer == CompileWallAssemblyLayer.Runtime &&
                                             target.Layer == CompileWallAssemblyLayer.Runtime
                    };

                    scan.Edges.Add(edge);
                    if (edge.IllegalRuntimeEdge)
                        scan.IllegalRuntimeEdges.Add(edge);
                }
            }

            DetectCycles(scan);
            BuildDomainHealth(scan);
            RunArchaeology(projectRoot, scan);
            ScanBeeArtifactSkews(projectRoot, scan);
            ScanRuntimePackViolations(projectRoot, scan);
            scan.ScanMilliseconds = (EditorApplication.timeSinceStartup - started) * 1000.0;
            CompileWallBlackBox.RecordGraphScan(scan);
            return scan;
        }

        private static CompileWallAsmdefNode TryReadNode(string projectRoot, string absolutePath)
        {
            try
            {
                if (!TryReadAsmdef(absolutePath, out CompileWallAsmdefData data) || string.IsNullOrEmpty(data.Name))
                    return null;

                string assetPath = ToProjectPath(projectRoot, absolutePath);
                return new CompileWallAsmdefNode
                {
                    Name = data.Name,
                    DisplayName = BuildDisplayName(data.Name),
                    Path = assetPath,
                    Guid = TryReadGuid(absolutePath + ".meta"),
                    References = data.References,
                    IncludePlatforms = data.IncludePlatforms,
                    Layer = Classify(data.Name, assetPath, data.IncludePlatforms)
                };
            }
            catch (Exception exception)
            {
                Debug.LogError("[CompileWall] Failed to parse asmdef " + absolutePath + ": " + exception.Message);
                return null;
            }
        }

        private static bool TryReadAsmdef(string absolutePath, out CompileWallAsmdefData data)
        {
            data = default;
            int length = ReadFileIntoScratch(absolutePath, AsmdefJsonScratch);
            if (length <= 0)
                return false;

            ReadOnlySpan<byte> json = new ReadOnlySpan<byte>(AsmdefJsonScratch, 0, length);
            data.Name = ReadJsonStringProperty(json, NameJsonProperty);
            data.References = ReadJsonStringArrayProperty(json, ReferencesJsonProperty);
            data.IncludePlatforms = ReadJsonStringArrayProperty(json, IncludePlatformsJsonProperty);
            return data.Name.Length > 0;
        }

        private static int ReadFileIntoScratch(string absolutePath, byte[] scratch)
        {
            int total = 0;
            using (var stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                while (total < scratch.Length)
                {
                    int bytesRead = stream.Read(scratch, total, scratch.Length - total);
                    if (bytesRead <= 0)
                        return total;

                    total += bytesRead;
                }

                return stream.ReadByte() < 0 ? total : -1;
            }
        }

        private static string ReadJsonStringProperty(ReadOnlySpan<byte> json, string property)
        {
            int propertyIndex = FindTopLevelJsonProperty(json, property);
            if (propertyIndex < 0)
                return string.Empty;

            int colon = FindByte(json, (byte)':', propertyIndex + property.Length);
            if (colon < 0)
                return string.Empty;

            int quote = FindByte(json, (byte)'"', colon + 1);
            if (quote < 0)
                return string.Empty;

            return ReadJsonStringAt(json, quote, out _);
        }

        private static string[] ReadJsonStringArrayProperty(ReadOnlySpan<byte> json, string property)
        {
            int propertyIndex = FindTopLevelJsonProperty(json, property);
            if (propertyIndex < 0)
                return Array.Empty<string>();

            int colon = FindByte(json, (byte)':', propertyIndex + property.Length);
            if (colon < 0)
                return Array.Empty<string>();

            int arrayStart = FindByte(json, (byte)'[', colon + 1);
            if (arrayStart < 0)
                return Array.Empty<string>();

            int count = CountJsonArrayStrings(json, arrayStart);
            if (count <= 0)
                return Array.Empty<string>();
            if (count > MaxAsmdefArrayItems)
                count = MaxAsmdefArrayItems;

            string[] values = new string[count];
            int written = 0;
            for (int i = arrayStart + 1; i < json.Length && written < count; i++)
            {
                byte value = json[i];
                if (value == (byte)']')
                    break;
                if (value != (byte)'"')
                    continue;

                values[written] = ReadJsonStringAt(json, i, out int afterString);
                written++;
                i = afterString - 1;
            }

            return values;
        }

        private static int CountJsonArrayStrings(ReadOnlySpan<byte> json, int arrayStart)
        {
            int count = 0;
            for (int i = arrayStart + 1; i < json.Length; i++)
            {
                byte value = json[i];
                if (value == (byte)']')
                    return count;
                if (value != (byte)'"')
                    continue;

                int afterString = SkipJsonStringAt(json, i);
                if (count < MaxAsmdefArrayItems)
                    count++;
                i = afterString - 1;
            }

            return count;
        }

        private static int SkipJsonStringAt(ReadOnlySpan<byte> json, int quoteIndex)
        {
            if (quoteIndex < 0 || quoteIndex >= json.Length || json[quoteIndex] != (byte)'"')
                return quoteIndex + 1;

            for (int i = quoteIndex + 1; i < json.Length; i++)
            {
                byte value = json[i];
                if (value == (byte)'"')
                    return i + 1;
                if (value == (byte)'\\' && i + 1 < json.Length)
                    i++;
            }

            return json.Length;
        }

        private static string ReadJsonStringAt(ReadOnlySpan<byte> json, int quoteIndex, out int afterString)
        {
            afterString = quoteIndex + 1;
            if (quoteIndex < 0 || quoteIndex >= json.Length || json[quoteIndex] != (byte)'"')
                return string.Empty;

            int length = 0;
            for (int i = quoteIndex + 1; i < json.Length; i++)
            {
                byte value = json[i];
                if (value == (byte)'"')
                {
                    afterString = i + 1;
                    return new string(IdentifierScratch, 0, length);
                }

                if (value == (byte)'\\' && i + 1 < json.Length)
                {
                    i++;
                    value = json[i];
                }

                if (length >= IdentifierScratch.Length)
                    return string.Empty;

                IdentifierScratch[length] = (char)value;
                length++;
            }

            return string.Empty;
        }

        private static int FindTopLevelJsonProperty(ReadOnlySpan<byte> json, string property)
        {
            int objectDepth = 0;
            int arrayDepth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = 0; i < json.Length; i++)
            {
                byte value = json[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (value == (byte)'\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (value == (byte)'"')
                        inString = false;
                    continue;
                }

                if (value == (byte)'"')
                {
                    if (objectDepth == 1 && arrayDepth == 0 && IsJsonPropertyAt(json, property, i))
                        return i;

                    inString = true;
                    continue;
                }

                if (value == (byte)'{')
                {
                    objectDepth++;
                    continue;
                }

                if (value == (byte)'}')
                {
                    if (objectDepth > 0)
                        objectDepth--;
                    continue;
                }

                if (value == (byte)'[')
                {
                    arrayDepth++;
                    continue;
                }

                if (value == (byte)']')
                {
                    if (arrayDepth > 0)
                        arrayDepth--;
                }
            }

            return -1;
        }

        private static bool IsJsonPropertyAt(ReadOnlySpan<byte> value, string token, int startIndex)
        {
            if (!MatchesAsciiAt(value, token, startIndex))
                return false;

            int colon = startIndex + token.Length;
            while (colon < value.Length && IsJsonWhitespace(value[colon]))
                colon++;

            return colon < value.Length && value[colon] == (byte)':';
        }

        private static bool IsJsonWhitespace(byte value)
        {
            return value == (byte)' ' ||
                   value == (byte)'\t' ||
                   value == (byte)'\r' ||
                   value == (byte)'\n';
        }

        private static bool MatchesAsciiAt(ReadOnlySpan<byte> value, string token, int startIndex)
        {
            if (token.Length == 0 || startIndex < 0 || startIndex + token.Length > value.Length)
                return false;

            for (int i = 0; i < token.Length; i++)
            {
                if (value[startIndex + i] != (byte)token[i])
                    return false;
            }

            return true;
        }

        private static int FindByte(ReadOnlySpan<byte> value, byte target, int startIndex)
        {
            for (int i = startIndex; i < value.Length; i++)
            {
                if (value[i] == target)
                    return i;
            }

            return -1;
        }

        private static string BuildDisplayName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            const string prefix = "Hecton8.";
            return name.StartsWith(prefix, StringComparison.Ordinal) ? name.Substring(prefix.Length) : name;
        }

        private static CompileWallAssemblyLayer Classify(string name, string assetPath, string[] includePlatforms)
        {
            if (ContainsToken(includePlatforms, "Editor") ||
                ContainsPathToken(assetPath, EditorPathToken, EditorBackslashPathToken) ||
                name.EndsWith(".Editor", StringComparison.Ordinal))
                return CompileWallAssemblyLayer.Editor;
            if (ContainsPathToken(assetPath, TestsPathToken, TestsBackslashPathToken) ||
                name.IndexOf("Tests", StringComparison.OrdinalIgnoreCase) >= 0)
                return CompileWallAssemblyLayer.Tests;
            if (ContainsPathToken(assetPath, ContractsPathToken, ContractsBackslashPathToken) ||
                name.EndsWith(".Contracts", StringComparison.Ordinal))
                return CompileWallAssemblyLayer.Contracts;
            if (ContainsPathToken(assetPath, AuthoringPathToken, AuthoringBackslashPathToken) ||
                name.EndsWith(".Authoring", StringComparison.Ordinal))
                return CompileWallAssemblyLayer.Authoring;
            return CompileWallAssemblyLayer.Runtime;
        }

        private static bool ContainsPathToken(string path, string forwardToken, string backslashToken)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            return path.IndexOf(forwardToken, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf(backslashToken, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsToken(string[] values, string token)
        {
            if (values == null)
                return false;

            for (int i = 0; i < values.Length; i++)
            {
                if (string.Equals(values[i], token, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static string ResolveReferenceName(
            string reference,
            Dictionary<string, CompileWallAsmdefNode> byGuidReference)
        {
            if (string.IsNullOrEmpty(reference))
                return string.Empty;

            if (reference.StartsWith(GuidReferencePrefix, StringComparison.Ordinal))
                return byGuidReference.TryGetValue(reference, out CompileWallAsmdefNode node) ? node.Name : string.Empty;

            return reference;
        }

        private static void DetectCycles(CompileWallGraphScan scan)
        {
            int count = scan.Nodes.Count;
            if (count <= 1)
                return;

            var indices = new Dictionary<string, int>(count);
            for (int i = 0; i < count; i++)
            {
                CompileWallAsmdefNode node = scan.Nodes[i];
                if (!string.IsNullOrEmpty(node.Name) && !indices.ContainsKey(node.Name))
                    indices.Add(node.Name, i);
            }

            var adjacency = new List<int>[count];
            for (int i = 0; i < scan.Edges.Count; i++)
            {
                CompileWallAsmdefEdge edge = scan.Edges[i];
                if (edge.From == null || edge.To == null)
                    continue;
                if (string.IsNullOrEmpty(edge.From.Name) || string.IsNullOrEmpty(edge.To.Name))
                    continue;
                if (!indices.TryGetValue(edge.From.Name, out int fromIndex) ||
                    !indices.TryGetValue(edge.To.Name, out int toIndex))
                    continue;

                if (adjacency[fromIndex] == null)
                    adjacency[fromIndex] = new List<int>(4);
                adjacency[fromIndex].Add(toIndex);
            }

            var state = new byte[count];
            var stack = new int[count];
            for (int i = 0; i < count; i++)
            {
                if (state[i] == 0)
                    VisitCycleNode(scan, adjacency, state, stack, i, 0);
                if (scan.CycleAssemblies.Count >= CompileWallGraphScan.MaxStoredCycleAssemblies)
                    return;
            }
        }

        private static void VisitCycleNode(
            CompileWallGraphScan scan,
            List<int>[] adjacency,
            byte[] state,
            int[] stack,
            int nodeIndex,
            int depth)
        {
            if (scan.CycleAssemblies.Count >= CompileWallGraphScan.MaxStoredCycleAssemblies)
                return;

            state[nodeIndex] = 1;
            stack[depth] = nodeIndex;
            List<int> targets = adjacency[nodeIndex];
            if (targets != null)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    int target = targets[i];
                    if (state[target] == 0)
                    {
                        VisitCycleNode(scan, adjacency, state, stack, target, depth + 1);
                    }
                    else if (state[target] == 1)
                    {
                        RecordCycleAssemblies(scan, stack, depth, target);
                    }

                    if (scan.CycleAssemblies.Count >= CompileWallGraphScan.MaxStoredCycleAssemblies)
                        return;
                }
            }

            state[nodeIndex] = 2;
        }

        private static void RecordCycleAssemblies(
            CompileWallGraphScan scan,
            int[] stack,
            int depth,
            int target)
        {
            for (int i = 0; i <= depth; i++)
            {
                if (stack[i] != target)
                    continue;

                for (int c = i; c <= depth; c++)
                    AddCycleAssembly(scan, scan.Nodes[stack[c]].Name);
                AddCycleAssembly(scan, scan.Nodes[target].Name);
                return;
            }
        }

        private static void AddCycleAssembly(CompileWallGraphScan scan, string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName) ||
                scan.CycleAssemblies.Count >= CompileWallGraphScan.MaxStoredCycleAssemblies)
                return;

            for (int i = 0; i < scan.CycleAssemblies.Count; i++)
            {
                if (string.Equals(scan.CycleAssemblies[i], assemblyName, StringComparison.Ordinal))
                    return;
            }

            scan.CycleAssemblies.Add(assemblyName);
        }

        private static void BuildDomainHealth(CompileWallGraphScan scan)
        {
            if (scan.Nodes.Count == 0)
                return;

            var byDomain = new Dictionary<string, CompileWallDomainHealth>(64);
            for (int i = 0; i < scan.Nodes.Count; i++)
            {
                CompileWallAsmdefNode node = scan.Nodes[i];
                CompileWallDomainHealth health = GetOrAddDomainHealth(scan, byDomain, node.Name);
                if (health == null)
                    continue;

                health.AssemblyCount++;
                if (node.Layer == CompileWallAssemblyLayer.Runtime)
                    health.RuntimeAssemblyCount++;
            }

            for (int i = 0; i < scan.IllegalRuntimeEdges.Count; i++)
            {
                CompileWallAsmdefEdge edge = scan.IllegalRuntimeEdges[i];
                if (edge.From == null)
                    continue;

                CompileWallDomainHealth health = GetOrAddDomainHealth(scan, byDomain, edge.From.Name);
                if (health != null)
                    health.IllegalRuntimeEdges++;
            }

            for (int i = 0; i < scan.CycleAssemblies.Count; i++)
            {
                CompileWallDomainHealth health = GetOrAddDomainHealth(scan, byDomain, scan.CycleAssemblies[i]);
                if (health != null)
                    health.CycleAssemblies++;
            }
        }

        private static CompileWallDomainHealth GetOrAddDomainHealth(
            CompileWallGraphScan scan,
            Dictionary<string, CompileWallDomainHealth> byDomain,
            string assemblyName)
        {
            string domainName = ExtractDomainName(assemblyName);
            if (byDomain.TryGetValue(domainName, out CompileWallDomainHealth health))
                return health;

            if (scan.DomainHealth.Count >= CompileWallGraphScan.MaxStoredDomainHealthRows)
                return null;

            health = new CompileWallDomainHealth
            {
                DomainName = domainName
            };
            byDomain.Add(domainName, health);
            scan.DomainHealth.Add(health);
            return health;
        }

        private static string ExtractDomainName(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
                return "UNKNOWN";

            const string prefix = "Hecton8.";
            int start = assemblyName.StartsWith(prefix, StringComparison.Ordinal) ? prefix.Length : 0;
            int end = assemblyName.IndexOf('.', start);
            if (end > start)
                return assemblyName.Substring(start, end - start);
            return start > 0 && start < assemblyName.Length ? assemblyName.Substring(start) : assemblyName;
        }

        private static void RunArchaeology(string projectRoot, CompileWallGraphScan scan)
        {
            ScanArchaeologyFolder(projectRoot, Path.Combine(projectRoot, ArchiveRoot.Replace('/', Path.DirectorySeparatorChar)), scan);
            ScanArchaeologyFolder(projectRoot, Path.Combine(projectRoot, StreamingAssetsRoot.Replace('/', Path.DirectorySeparatorChar)), scan);
            if (scan.ArchaeologyFiles.Count > 0)
                return;

            scan.UsedEmergencyMockAtlas = true;
            GenerateEmergencyMockAtlas(scan);
        }

        private static void ScanArchaeologyFolder(string projectRoot, string absoluteRoot, CompileWallGraphScan scan)
        {
            if (!Directory.Exists(absoluteRoot))
                return;

            using (IEnumerator<string> files = Directory.EnumerateFiles(
                       absoluteRoot,
                       "*.*",
                       SearchOption.AllDirectories).GetEnumerator())
            {
                while (files.MoveNext())
                {
                    string path = files.Current;
                    if (FileNameStartsWithAndEndsWith(path, LegacyGraphFilePrefix, LegacyGraphFileSuffix))
                    {
                        scan.ArchaeologyFiles.Add(ToProjectPath(projectRoot, path));
                        if (TryReadLegacyGraphHeader(path, out CompileWallLegacyGraphHeader header))
                            scan.LegacyGraphHeaders.Add(header);
                    }
                    else if (FileNameEquals(path, ProjectAtlasFileName))
                        scan.ArchaeologyFiles.Add(ToProjectPath(projectRoot, path));
                }
            }
        }

        private static bool TryReadLegacyGraphHeader(string absolutePath, out CompileWallLegacyGraphHeader header)
        {
            header = default;
            try
            {
                using (var stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (stream.Length < LegacyGraphHeaderBytes)
                        return false;

                    int total = 0;
                    while (total < LegacyGraphHeaderBytes)
                    {
                        int read = stream.Read(LegacyHeaderScratch, total, LegacyGraphHeaderBytes - total);
                        if (read <= 0)
                            return false;

                        total += read;
                    }
                }

                uint littleMagic = ReadUInt32Little(LegacyHeaderScratch, 0);
                uint bigMagic = ReverseUInt32(littleMagic);
                ushort littleHeaderBytes = ReadUInt16Little(LegacyHeaderScratch, 6);
                ushort bigHeaderBytes = ReadUInt16Big(LegacyHeaderScratch, 6);
                bool littleHeaderPlausible = IsPlausibleLegacyHeaderBytes(littleHeaderBytes);
                bool bigHeaderPlausible = IsPlausibleLegacyHeaderBytes(bigHeaderBytes);
                bool bigEndian = bigHeaderPlausible && !littleHeaderPlausible;
                bool recognizedMagic = littleMagic == LegacyGraphMagic || bigMagic == LegacyGraphMagic;

                header.Magic = recognizedMagic ? LegacyGraphMagic : littleMagic;
                header.Version = ReadUInt16(LegacyHeaderScratch, 4, bigEndian);
                header.HeaderBytes = ReadUInt16(LegacyHeaderScratch, 6, bigEndian);
                header.NodeCount = ReadUInt32(LegacyHeaderScratch, 8, bigEndian);
                header.EdgeCount = ReadUInt32(LegacyHeaderScratch, 12, bigEndian);
                header.Flags = ReadUInt32(LegacyHeaderScratch, 16, bigEndian);
                header.PayloadBytes = ReadUInt32(LegacyHeaderScratch, 20, bigEndian);
                header.Crc32 = ReadUInt32(LegacyHeaderScratch, 24, bigEndian);
                header.Reserved = ReadUInt32(LegacyHeaderScratch, 28, bigEndian);
                if (bigEndian)
                    header.Flags |= LegacyGraphHeaderFlagBigEndian;
                if (recognizedMagic)
                    header.Flags |= LegacyGraphHeaderFlagRecognizedMagic;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[CompileWall] Failed to read legacy asmdef graph header " + absolutePath + ": " + exception.Message);
                return false;
            }
        }

        private static bool IsPlausibleLegacyHeaderBytes(ushort headerBytes)
        {
            return headerBytes >= LegacyGraphHeaderBytes && headerBytes <= 4096 && (headerBytes & 7) == 0;
        }

        private static ushort ReadUInt16(byte[] data, int offset, bool bigEndian)
        {
            return bigEndian ? ReadUInt16Big(data, offset) : ReadUInt16Little(data, offset);
        }

        private static ushort ReadUInt16Little(byte[] data, int offset)
        {
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }

        private static ushort ReadUInt16Big(byte[] data, int offset)
        {
            return (ushort)((data[offset] << 8) | data[offset + 1]);
        }

        private static uint ReadUInt32(byte[] data, int offset, bool bigEndian)
        {
            uint value = ReadUInt32Little(data, offset);
            return bigEndian ? ReverseUInt32(value) : value;
        }

        private static uint ReadUInt32Little(byte[] data, int offset)
        {
            return (uint)(data[offset] |
                          (data[offset + 1] << 8) |
                          (data[offset + 2] << 16) |
                          (data[offset + 3] << 24));
        }

        private static uint ReverseUInt32(uint value)
        {
            return ((value & 0x000000FFu) << 24) |
                   ((value & 0x0000FF00u) << 8) |
                   ((value & 0x00FF0000u) >> 8) |
                   ((value & 0xFF000000u) >> 24);
        }

        private static void GenerateEmergencyMockAtlas(CompileWallGraphScan scan)
        {
            scan.MockAtlasAssemblies.Add("Hecton8.Global.Contracts");
            scan.MockAtlasAssemblies.Add("Hecton8.MockDomain.Contracts");
            scan.MockAtlasAssemblies.Add("Hecton8.MockDomain.Runtime");
            scan.MockAtlasAssemblies.Add("Hecton8.Core.Contracts");
            scan.MockAtlasAssemblies.Add("Hecton8.World.Contracts");
            scan.MockAtlasAssemblies.Add("Hecton8.UI.Diegetic.Contracts");
            scan.MockAtlasAssemblies.Add("Hecton8.Vehicles.Physics.Contracts");
        }

        private static void ScanBeeArtifactSkews(string projectRoot, CompileWallGraphScan scan)
        {
            string beeArtifactsRoot = Path.Combine(projectRoot, "Library", "Bee", "artifacts");
            if (!Directory.Exists(beeArtifactsRoot))
                return;

            Dictionary<string, CompileWallRefArtifactRecord> refArtifactPaths = BuildRefArtifactIndex(beeArtifactsRoot);
            for (int i = 0; i < scan.Edges.Count; i++)
            {
                CompileWallAsmdefEdge edge = scan.Edges[i];
                if (edge.To.Layer != CompileWallAssemblyLayer.Contracts ||
                    edge.From.Layer == CompileWallAssemblyLayer.Contracts)
                    continue;

                if (!refArtifactPaths.TryGetValue(edge.From.Name, out CompileWallRefArtifactRecord fromRecord) ||
                    !refArtifactPaths.TryGetValue(edge.To.Name, out CompileWallRefArtifactRecord toRecord))
                    continue;

                if (fromRecord.WriteTimeUtcTicks >= toRecord.WriteTimeUtcTicks)
                    continue;

                scan.ArtifactSkews.Add(new CompileWallArtifactSkew
                {
                    From = edge.From,
                    To = edge.To,
                    FromRefPath = ToProjectPath(projectRoot, fromRecord.Path),
                    ToRefPath = ToProjectPath(projectRoot, toRecord.Path),
                    SecondsBehind = new TimeSpan(toRecord.WriteTimeUtcTicks - fromRecord.WriteTimeUtcTicks).TotalSeconds
                });
            }
        }

        private static Dictionary<string, CompileWallRefArtifactRecord> BuildRefArtifactIndex(string beeArtifactsRoot)
        {
            var result = new Dictionary<string, CompileWallRefArtifactRecord>(256);
            using (IEnumerator<string> files = Directory.EnumerateFiles(
                       beeArtifactsRoot,
                       "*.ref.dll",
                       SearchOption.AllDirectories).GetEnumerator())
            {
                while (files.MoveNext())
                {
                    string path = files.Current;
                    if (path.IndexOf(PostProcessedPathToken, StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    if (!TryGetRefAssemblyName(path, out string assemblyName))
                        continue;

                    long writeTimeTicks = File.GetLastWriteTimeUtc(path).Ticks;
                    if (!result.TryGetValue(assemblyName, out CompileWallRefArtifactRecord existing) ||
                        writeTimeTicks > existing.WriteTimeUtcTicks)
                    {
                        result[assemblyName] = new CompileWallRefArtifactRecord
                        {
                            Path = path,
                            WriteTimeUtcTicks = writeTimeTicks
                        };
                    }
                }
            }

            return result;
        }

        private static bool TryGetRefAssemblyName(string path, out string assemblyName)
        {
            assemblyName = string.Empty;
            if (string.IsNullOrEmpty(path) || !path.EndsWith(RefDllSuffix, StringComparison.Ordinal))
                return false;

            int suffixStart = path.Length - RefDllSuffix.Length;
            int nameStart = LastIndexOfDirectorySeparator(path) + 1;
            if (nameStart >= suffixStart)
                return false;

            assemblyName = path.Substring(nameStart, suffixStart - nameStart);
            return true;
        }

        private static int LastIndexOfDirectorySeparator(string path)
        {
            for (int i = path.Length - 1; i >= 0; i--)
            {
                char value = path[i];
                if (value == '/' || value == '\\')
                    return i;
            }

            return -1;
        }

        private static bool FileNameEquals(string path, string expected)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(expected))
                return false;

            int nameStart = LastIndexOfDirectorySeparator(path) + 1;
            int nameLength = path.Length - nameStart;
            return nameLength == expected.Length &&
                   string.Compare(path, nameStart, expected, 0, expected.Length, StringComparison.OrdinalIgnoreCase) == 0;
        }

        private static bool FileNameStartsWithAndEndsWith(string path, string prefix, string suffix)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(suffix))
                return false;

            int nameStart = LastIndexOfDirectorySeparator(path) + 1;
            int nameLength = path.Length - nameStart;
            if (nameLength < prefix.Length + suffix.Length)
                return false;

            return string.Compare(path, nameStart, prefix, 0, prefix.Length, StringComparison.OrdinalIgnoreCase) == 0 &&
                   string.Compare(
                       path,
                       path.Length - suffix.Length,
                       suffix,
                       0,
                       suffix.Length,
                       StringComparison.OrdinalIgnoreCase) == 0;
        }

        private static void ScanRuntimePackViolations(string projectRoot, CompileWallGraphScan scan)
        {
            string sourceRoot = Path.Combine(projectRoot, "Assets", "_Project", "Scripts");
            if (!Directory.Exists(sourceRoot))
                return;

            string editorSegment = Path.DirectorySeparatorChar + "Editor" + Path.DirectorySeparatorChar;
            using (IEnumerator<string> files = Directory.EnumerateFiles(
                       sourceRoot,
                       "*.cs",
                       SearchOption.AllDirectories).GetEnumerator())
            {
                while (files.MoveNext())
                {
                    string path = files.Current;
                    if (path.IndexOf(editorSegment, StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    ScanFileForPackOne(projectRoot, path, scan);
                }
            }
        }

        private static void ScanFileForPackOne(string projectRoot, string absolutePath, CompileWallGraphScan scan)
        {
            try
            {
                using (var stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int lineNumber = 1;
                    int attributeLineNumber = 1;
                    int structLayoutMatch = 0;
                    int packMatch = 0;
                    int packState = 0;
                    bool inLineComment = false;
                    bool inBlockComment = false;
                    bool inAttribute = false;
                    bool attributeHasStructLayout = false;
                    bool attributeHasPackOne = false;
                    string projectPath = null;
                    byte previous = 0;
                    int bytesRead;

                    while ((bytesRead = stream.Read(SourceScanScratch, 0, SourceScanScratch.Length)) > 0)
                    {
                        for (int i = 0; i < bytesRead; i++)
                        {
                            byte value = SourceScanScratch[i];
                            bool isNewLine = value == (byte)'\n';

                            if (!inAttribute)
                            {
                                if (inLineComment)
                                {
                                    if (isNewLine)
                                    {
                                        lineNumber++;
                                        inLineComment = false;
                                    }

                                    previous = value;
                                    continue;
                                }

                                if (inBlockComment)
                                {
                                    if (previous == (byte)'*' && value == (byte)'/')
                                        inBlockComment = false;
                                    if (isNewLine)
                                        lineNumber++;
                                    previous = value;
                                    continue;
                                }

                                if (previous == (byte)'/' && value == (byte)'/')
                                {
                                    inLineComment = true;
                                    previous = value;
                                    continue;
                                }

                                if (previous == (byte)'/' && value == (byte)'*')
                                {
                                    inBlockComment = true;
                                    previous = value;
                                    continue;
                                }

                                if (value == (byte)'[')
                                {
                                    inAttribute = true;
                                    attributeLineNumber = lineNumber;
                                    structLayoutMatch = 0;
                                    packMatch = 0;
                                    packState = 0;
                                    attributeHasStructLayout = false;
                                    attributeHasPackOne = false;
                                    previous = value;
                                    continue;
                                }

                                if (isNewLine)
                                    lineNumber++;
                                previous = value;
                                continue;
                            }

                            if (packState == 3)
                            {
                                if (!IsAsciiDigit(value))
                                    attributeHasPackOne = true;
                                packState = 0;
                                packMatch = 0;
                            }

                            if (value == (byte)']')
                            {
                                if (attributeHasStructLayout && attributeHasPackOne)
                                    RecordPackViolation(projectRoot, absolutePath, attributeLineNumber, scan, ref projectPath);

                                inAttribute = false;
                                structLayoutMatch = 0;
                                packMatch = 0;
                                packState = 0;
                                if (isNewLine)
                                    lineNumber++;
                                previous = value;
                                continue;
                            }

                            structLayoutMatch = AdvanceToken(value, StructLayoutToken, structLayoutMatch, out bool structComplete);
                            if (structComplete)
                                attributeHasStructLayout = true;

                            AdvancePackOneDetector(value, ref packMatch, ref packState, ref attributeHasPackOne);
                            if (isNewLine)
                                lineNumber++;
                            previous = value;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[CompileWall] Failed to scan struct layout directives: " + absolutePath + " " + exception.Message);
            }
        }

        private static int AdvanceToken(byte value, string token, int index, out bool complete)
        {
            complete = false;
            char current = (char)value;
            if (current == token[index])
            {
                index++;
                if (index == token.Length)
                {
                    complete = true;
                    return 0;
                }

                return index;
            }

            return current == token[0] ? 1 : 0;
        }

        private static void AdvancePackOneDetector(byte value, ref int packMatch, ref int packState, ref bool complete)
        {
            if (complete)
                return;

            if (packState == 0)
            {
                packMatch = AdvanceToken(value, PackToken, packMatch, out bool tokenComplete);
                if (tokenComplete)
                    packState = 1;
                return;
            }

            if (packState == 1)
            {
                if (IsAsciiWhitespace(value))
                    return;
                if (value == (byte)'=')
                {
                    packState = 2;
                    return;
                }

                packMatch = 0;
                packState = 0;
                return;
            }

            if (packState == 2)
            {
                if (IsAsciiWhitespace(value))
                    return;
                if (value == (byte)'1')
                {
                    packState = 3;
                    return;
                }

                packMatch = 0;
                packState = 0;
            }
        }

        private static void RecordPackViolation(
            string projectRoot,
            string absolutePath,
            int lineNumber,
            CompileWallGraphScan scan,
            ref string projectPath)
        {
            scan.PackViolationTotal++;
            if (scan.PackViolations.Count >= CompileWallGraphScan.MaxStoredPackViolations)
                return;

            if (string.IsNullOrEmpty(projectPath))
                projectPath = ToProjectPath(projectRoot, absolutePath);

            scan.PackViolations.Add(new CompileWallPackViolation
            {
                Path = projectPath,
                LineNumber = lineNumber,
                Snippet = PackOneSnippet
            });
        }

        private static bool IsAsciiWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t' || value == (byte)'\r' || value == (byte)'\n';
        }

        private static bool IsAsciiDigit(byte value)
        {
            return value >= (byte)'0' && value <= (byte)'9';
        }

        private static bool IsAsciiHex(byte value)
        {
            return (value >= (byte)'0' && value <= (byte)'9') ||
                   (value >= (byte)'a' && value <= (byte)'f') ||
                   (value >= (byte)'A' && value <= (byte)'F');
        }

        private static bool IsMetaGuidSpacer(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t';
        }

        private static string TryReadGuid(string metaPath)
        {
            if (!File.Exists(metaPath))
                return string.Empty;

            try
            {
                using (var stream = new FileStream(metaPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int tokenMatch = 0;
                    int guidLength = 0;
                    bool readingGuid = false;
                    int bytesRead;

                    while ((bytesRead = stream.Read(SourceScanScratch, 0, SourceScanScratch.Length)) > 0)
                    {
                        for (int i = 0; i < bytesRead; i++)
                        {
                            byte value = SourceScanScratch[i];
                            if (!readingGuid)
                            {
                                tokenMatch = AdvanceToken(value, GuidToken, tokenMatch, out bool tokenComplete);
                                if (tokenComplete)
                                    readingGuid = true;
                                continue;
                            }

                            if (guidLength == 0 && IsMetaGuidSpacer(value))
                                continue;

                            if (IsAsciiHex(value))
                            {
                                if (guidLength >= UnityGuidHexLength)
                                    return string.Empty;

                                GuidScratch[guidLength] = (char)value;
                                guidLength++;
                                if (guidLength == UnityGuidHexLength)
                                    return new string(GuidScratch, 0, UnityGuidHexLength);
                                continue;
                            }

                            return string.Empty;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[CompileWall] Failed to read asmdef meta guid: " + metaPath + " " + exception.Message);
            }

            return string.Empty;
        }

        private static string GetProjectRoot()
        {
            string assetsPath = Application.dataPath.Replace('\\', '/');
            int index = assetsPath.LastIndexOf("/Assets", StringComparison.Ordinal);
            return index > 0 ? assetsPath.Substring(0, index) : Directory.GetCurrentDirectory().Replace('\\', '/');
        }

        private static string ToProjectPath(string projectRoot, string absolutePath)
        {
            int rootLength = TrimTrailingSeparators(projectRoot);
            if (HasPathPrefix(absolutePath, projectRoot, rootLength))
                return NormalizeRelativePath(absolutePath, rootLength + 1);

            return NormalizeRelativePath(absolutePath, 0);
        }

        private static int TrimTrailingSeparators(string path)
        {
            int length = string.IsNullOrEmpty(path) ? 0 : path.Length;
            while (length > 0 && IsDirectorySeparator(path[length - 1]))
                length--;

            return length;
        }

        private static bool HasPathPrefix(string path, string root, int rootLength)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(root) || rootLength <= 0)
                return false;
            if (path.Length <= rootLength || !IsDirectorySeparator(path[rootLength]))
                return false;

            for (int i = 0; i < rootLength; i++)
            {
                char pathChar = path[i];
                char rootChar = root[i];
                if (IsDirectorySeparator(pathChar) && IsDirectorySeparator(rootChar))
                    continue;
                if (char.ToUpperInvariant(pathChar) != char.ToUpperInvariant(rootChar))
                    return false;
            }

            return true;
        }

        private static string NormalizeRelativePath(string path, int start)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            int safeStart = start < 0 ? 0 : start;
            if (safeStart > path.Length)
                safeStart = path.Length;
            string relative = path.Substring(safeStart);
            return relative.IndexOf('\\') >= 0 ? relative.Replace('\\', '/') : relative;
        }

        private static bool IsDirectorySeparator(char value)
        {
            return value == '/' || value == '\\';
        }
    }

    internal sealed class CompileWallAsmdefBuildGate : IPreprocessBuildWithReport
    {
        public int callbackOrder => -8800;

        public void OnPreprocessBuild(BuildReport report)
        {
            CompileWallGeneratedArtifacts.EnsureGeneratedArtifacts();
            CompileWallGraphScan scan = CompileWallAssemblyGraphScanner.ScanProject();
            if (scan.IllegalRuntimeEdges.Count == 0 && scan.CycleAssemblies.Count == 0)
                return;

            int limit = scan.IllegalRuntimeEdges.Count < 80 ? scan.IllegalRuntimeEdges.Count : 80;
            if (scan.IllegalRuntimeEdges.Count > 0)
            {
                Debug.LogError("[CompileWall] Build blocked: Runtime assembly references another Runtime assembly directly.");
                for (int i = 0; i < limit; i++)
                {
                    CompileWallAsmdefEdge edge = scan.IllegalRuntimeEdges[i];
                    Debug.LogError("[CompileWall] Runtime -> Runtime edge source assembly:");
                    Debug.LogError(edge.From.Name);
                    Debug.LogError("[CompileWall] Runtime -> Runtime edge target assembly:");
                    Debug.LogError(edge.To.Name);
                    Debug.LogError("[CompileWall] Runtime -> Runtime edge source path:");
                    Debug.LogError(edge.From.Path);
                }

                if (scan.IllegalRuntimeEdges.Count > limit)
                    Debug.LogError("[CompileWall] Illegal edge list truncated. Open Compile Wall X-Ray for the complete graph.");
            }

            int cycleLimit = scan.CycleAssemblies.Count < 80 ? scan.CycleAssemblies.Count : 80;
            if (scan.CycleAssemblies.Count > 0)
            {
                Debug.LogError("[CompileWall] Build blocked: asmdef dependency cycle detected.");
                for (int i = 0; i < cycleLimit; i++)
                {
                    string assemblyName = scan.CycleAssemblies[i];
                    CompileWallAsmdefNode node = scan.FindNodeByName(assemblyName);
                    Debug.LogError("[CompileWall] Cycle assembly:");
                    Debug.LogError(assemblyName);
                    if (node != null)
                    {
                        Debug.LogError("[CompileWall] Cycle assembly path:");
                        Debug.LogError(node.Path);
                    }
                }

                if (scan.CycleAssemblies.Count > cycleLimit)
                    Debug.LogError("[CompileWall] Cycle assembly list truncated. Open Compile Wall X-Ray for the stored rows.");
            }

            CompileWallBlackBox.Dump("AsmdefCompileWallBreach", scan);
            const string message = "[CompileWall] Build blocked by asmdef compile-wall breach.";
            throw new BuildFailedException(message);
        }
    }

    [InitializeOnLoad]
    internal static class CompileWallTelemetryRecorder
    {
        private const int SampleCapacity = 300;
        private const double WarningThresholdSeconds = 2.0;

        private static readonly CompileWallCompilationSample[] _samples = new CompileWallCompilationSample[SampleCapacity];
        private static int _sampleHead;
        private static double _compilationWindowStart;
        private static double _lastAssemblySampleTime;

        static CompileWallTelemetryRecorder()
        {
            CompilationPipeline.compilationStarted -= HandleCompilationStarted;
            CompilationPipeline.compilationStarted += HandleCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished -= HandleAssemblyCompilationFinished;
            CompilationPipeline.assemblyCompilationFinished += HandleAssemblyCompilationFinished;
            CompilationPipeline.compilationFinished -= HandleCompilationFinished;
            CompilationPipeline.compilationFinished += HandleCompilationFinished;
        }

        public static int CopySamples(CompileWallCompilationSample[] destination)
        {
            if (destination == null)
                return 0;

            int count = destination.Length < SampleCapacity ? destination.Length : SampleCapacity;
            for (int i = 0; i < count; i++)
            {
                int sourceIndex = (_sampleHead + i) % SampleCapacity;
                destination[i] = _samples[sourceIndex];
            }

            return count;
        }

        private static void HandleCompilationStarted(object context)
        {
            double now = EditorApplication.timeSinceStartup;
            _compilationWindowStart = now;
            _lastAssemblySampleTime = now;
        }

        private static void HandleCompilationFinished(object context)
        {
            _compilationWindowStart = 0d;
            _lastAssemblySampleTime = 0d;
        }

        private static void HandleAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            double now = EditorApplication.timeSinceStartup;
            double started = _lastAssemblySampleTime > 0d
                ? _lastAssemblySampleTime
                : (_compilationWindowStart > 0d ? _compilationWindowStart : now);
            double elapsed = now - started;
            _lastAssemblySampleTime = now;

            int warningCount = 0;
            int errorCount = 0;
            if (messages != null)
            {
                for (int i = 0; i < messages.Length; i++)
                {
                    if (messages[i].type == CompilerMessageType.Error)
                        errorCount++;
                    else if (messages[i].type == CompilerMessageType.Warning)
                        warningCount++;
                }
            }

            var sample = new CompileWallCompilationSample(assemblyPath, elapsed, warningCount, errorCount);
            _samples[_sampleHead] = sample;
            _sampleHead = (_sampleHead + 1) % SampleCapacity;
            CompileWallBlackBox.RecordCompilationSample(sample);

            if (elapsed > WarningThresholdSeconds)
            {
                Debug.LogWarning("[CompileWall] Slow assembly compile threshold exceeded.");
                Debug.LogWarning(sample.AssemblyLabel);
            }
        }

    }

    [StructLayout(LayoutKind.Explicit, Size = 64, Pack = 8)]
    internal struct CompileWallBlackBoxEntry
    {
        [FieldOffset(0)] public double Seconds;
        [FieldOffset(8)] public int Sequence;
        [FieldOffset(12)] public int NodeCount;
        [FieldOffset(16)] public int EdgeCount;
        [FieldOffset(20)] public int IllegalEdgeCount;
        [FieldOffset(24)] public int WarningCount;
        [FieldOffset(28)] public int ErrorCount;
        [FieldOffset(32)] public uint EventHash;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public int DomainHealthRows;
        [FieldOffset(44)] public int Reserved0;
        [FieldOffset(48)] public long Reserved1;
        [FieldOffset(56)] public long Reserved2;
    }

    [InitializeOnLoad]
    internal static class CompileWallBlackBox
    {
        private const int Capacity = 300;
        private const int DumpVersion = 6;
        private const int EntryBytes = 64;
        private const uint DumpMagic = 0x48384450u;
        private const uint EventGraphScan = 0x47524150u;
        private const uint EventCompileSample = 0x434F4D50u;
        private const uint GraphFlagEmergencyMockAtlas = 1u;
        private const uint GraphFlagLegacyGraphHeaders = 2u;
        private const uint GraphFlagStaleContractArtifact = 4u;
        private const uint GraphFlagPackOneRuntimeStruct = 8u;
        private const uint GraphFlagAsmdefCycle = 16u;
        private const string DumpAssetPath = "Docs/AgentLogs/Dump_SHINOBU_31.h8dump";

        private static NativeArray<CompileWallBlackBoxEntry> _entries;
        private static int _head;
        private static int _sequence;

        static CompileWallBlackBox()
        {
            EnsureAllocated();
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            EditorApplication.quitting -= Dispose;
            EditorApplication.quitting += Dispose;
        }

        public static void RecordGraphScan(CompileWallGraphScan scan)
        {
            if (scan == null)
                return;

            EnsureAllocated();
            var entry = new CompileWallBlackBoxEntry
            {
                Seconds = scan.ScanMilliseconds * 0.001,
                Sequence = _sequence++,
                NodeCount = scan.Nodes.Count,
                EdgeCount = scan.Edges.Count,
                IllegalEdgeCount = scan.IllegalRuntimeEdges.Count,
                WarningCount = scan.ArtifactSkews.Count + scan.PackViolationTotal + scan.CycleAssemblies.Count,
                EventHash = EventGraphScan,
                DomainHealthRows = scan.DomainHealth.Count,
                Flags = (scan.UsedEmergencyMockAtlas ? GraphFlagEmergencyMockAtlas : 0u) |
                        (scan.LegacyGraphHeaders.Count > 0 ? GraphFlagLegacyGraphHeaders : 0u) |
                        (scan.ArtifactSkews.Count > 0 ? GraphFlagStaleContractArtifact : 0u) |
                        (scan.PackViolationTotal > 0 ? GraphFlagPackOneRuntimeStruct : 0u) |
                        (scan.CycleAssemblies.Count > 0 ? GraphFlagAsmdefCycle : 0u)
            };
            Write(entry);
            if (double.IsNaN(entry.Seconds) || double.IsInfinity(entry.Seconds))
                Dump("ScanTimeInvalid", scan);
        }

        public static void RecordCompilationSample(CompileWallCompilationSample sample)
        {
            EnsureAllocated();
            var entry = new CompileWallBlackBoxEntry
            {
                Seconds = sample.Seconds,
                Sequence = _sequence++,
                WarningCount = sample.WarningCount,
                ErrorCount = sample.ErrorCount,
                EventHash = HashPath(sample.AssemblyPath),
                Flags = EventCompileSample
            };
            Write(entry);
            if (double.IsNaN(entry.Seconds) || double.IsInfinity(entry.Seconds))
                Dump("CompileTimeInvalid", null);
        }

        public static void Dump(string reason, CompileWallGraphScan scan)
        {
            EnsureAllocated();
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string absolutePath = Path.Combine(projectRoot, DumpAssetPath.Replace('/', Path.DirectorySeparatorChar));
            string directory = Path.GetDirectoryName(absolutePath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using (var stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(DumpMagic);
                writer.Write(DumpVersion);
                writer.Write(reason ?? string.Empty);
                writer.Write(_sequence);
                writer.Write(Capacity);
                writer.Write(EntryBytes);
                writer.Write(scan != null ? scan.Nodes.Count : 0);
                writer.Write(scan != null ? scan.Edges.Count : 0);
                writer.Write(scan != null ? scan.IllegalRuntimeEdges.Count : 0);
                writer.Write(scan != null ? scan.CycleAssemblies.Count : 0);
                writer.Write(scan != null ? scan.ArtifactSkews.Count : 0);
                writer.Write(scan != null ? scan.PackViolationTotal : 0);
                writer.Write(scan != null ? scan.PackViolations.Count : 0);
                for (int i = 0; i < Capacity; i++)
                {
                    int index = (_head + i) % Capacity;
                    WriteEntry(writer, _entries[index]);
                }
            }
        }

        private static void EnsureAllocated()
        {
            if (!_entries.IsCreated)
                _entries = new NativeArray<CompileWallBlackBoxEntry>(Capacity, Allocator.Persistent);
        }

        private static void Dispose()
        {
            if (_entries.IsCreated)
                _entries.Dispose();
        }

        private static void Write(CompileWallBlackBoxEntry entry)
        {
            _entries[_head] = entry;
            _head = (_head + 1) % Capacity;
        }

        private static void WriteEntry(BinaryWriter writer, CompileWallBlackBoxEntry entry)
        {
            writer.Write(entry.Seconds);
            writer.Write(entry.Sequence);
            writer.Write(entry.NodeCount);
            writer.Write(entry.EdgeCount);
            writer.Write(entry.IllegalEdgeCount);
            writer.Write(entry.WarningCount);
            writer.Write(entry.ErrorCount);
            writer.Write(entry.EventHash);
            writer.Write(entry.Flags);
            writer.Write(entry.DomainHealthRows);
            writer.Write(entry.Reserved0);
            writer.Write(entry.Reserved1);
            writer.Write(entry.Reserved2);
        }

        private static uint HashPath(string path)
        {
            const uint offset = 2166136261u;
            const uint prime = 16777619u;
            uint hash = offset;
            if (string.IsNullOrEmpty(path))
                return hash;

            for (int i = 0; i < path.Length; i++)
            {
                hash ^= path[i];
                hash *= prime;
            }

            return hash;
        }
    }

    internal readonly struct CompileWallCompilationSample
    {
        public readonly string AssemblyPath;
        public readonly string AssemblyLabel;
        public readonly double Seconds;
        public readonly int WarningCount;
        public readonly int ErrorCount;

        public CompileWallCompilationSample(string assemblyPath, double seconds, int warningCount, int errorCount)
        {
            AssemblyPath = assemblyPath ?? string.Empty;
            AssemblyLabel = AssemblyPath;
            Seconds = seconds;
            WarningCount = warningCount;
            ErrorCount = errorCount;
        }
    }

    internal static class CompileWallGeneratedArtifacts
    {
        private const string LinkXmlPath = "Assets/_Project/Scripts/Global/Generated/link.xml";
        private const string VaultOffsetsPath = "Assets/_Project/Scripts/Global/Contracts/Generated/VaultOffsets.g.cs";

        [MenuItem("Tools/Hecton-8/Compile Wall/Generate Link And Offsets")]
        public static void GenerateAll()
        {
            EnsureGeneratedArtifacts();
            AssetDatabase.Refresh();
        }

        public static void EnsureGeneratedArtifacts()
        {
            WriteLinkXmlIfChanged();
            WriteVaultOffsetsIfChanged();
        }

        private static void WriteLinkXmlIfChanged()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string absolutePath = Path.Combine(projectRoot, LinkXmlPath.Replace('/', Path.DirectorySeparatorChar));
            string directory = Path.GetDirectoryName(absolutePath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string tempPath = absolutePath + ".tmp";
            using (var writer = new StreamWriter(tempPath, false, Encoding.ASCII))
            {
                WriteLinkXml(writer);
            }

            if (File.Exists(absolutePath) && FilesEqual(absolutePath, tempPath))
            {
                File.Delete(tempPath);
                return;
            }

            File.Copy(tempPath, absolutePath, true);
            File.Delete(tempPath);
        }

        private static void WriteLinkXml(TextWriter writer)
        {
            var preservedTypes = new HashSet<string>(128);
            writer.WriteLine("<linker>");
            writer.WriteLine("  <assembly fullname=\"Hecton8.Global.Contracts\" preserve=\"all\" />");
            writer.WriteLine("  <assembly fullname=\"Hecton8.MockDomain.Contracts\" preserve=\"all\" />");
            writer.WriteLine("  <assembly fullname=\"Hecton8.MockDomain.Runtime\" preserve=\"all\" />");
            writer.WriteLine("  <assembly fullname=\"Hecton8.Core\" preserve=\"nothing\">");
            AppendPreservedType(writer, preservedTypes, "Hecton8.Core.GlobalRegistry");
            AppendTypesFromSource(writer, preservedTypes, "Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs");
            AppendTypesFromSource(writer, preservedTypes, "Assets/_Project/Scripts/Core/GlobalRegistry.cs");
            writer.WriteLine("  </assembly>");
            writer.WriteLine("</linker>");
        }

        private static void AppendTypesFromSource(TextWriter writer, HashSet<string> preservedTypes, string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string absolutePath = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolutePath))
                return;

            string currentNamespace = string.Empty;
            int lineLength = 0;
            try
            {
                using (var stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int bytesRead;
                    while ((bytesRead = stream.Read(SourceScanScratch, 0, SourceScanScratch.Length)) > 0)
                    {
                        for (int i = 0; i < bytesRead; i++)
                        {
                            byte value = SourceScanScratch[i];
                            if (value == (byte)'\n')
                            {
                                ProcessLinkSourceLine(writer, preservedTypes, TypeLineScratch, lineLength, ref currentNamespace);
                                lineLength = 0;
                                continue;
                            }

                            if (value == (byte)'\r')
                                continue;

                            if (lineLength < TypeLineScratch.Length)
                            {
                                TypeLineScratch[lineLength] = value;
                                lineLength++;
                            }
                        }
                    }

                    if (lineLength > 0)
                        ProcessLinkSourceLine(writer, preservedTypes, TypeLineScratch, lineLength, ref currentNamespace);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[CompileWall] Failed to parse link preservation source: " + absolutePath + " " + exception.Message);
            }
        }

        private static void ProcessLinkSourceLine(
            TextWriter writer,
            HashSet<string> preservedTypes,
            byte[] lineScratch,
            int lineLength,
            ref string currentNamespace)
        {
            if (lineLength <= 0)
                return;

            ReadOnlySpan<byte> rawLine = new ReadOnlySpan<byte>(lineScratch, 0, lineLength);
            ReadOnlySpan<byte> line = Trim(rawLine);
            if (StartsWithAscii(line, NamespaceToken))
            {
                currentNamespace = CreateAsciiString(Trim(line.Slice(NamespaceToken.Length)));
                return;
            }

            if (!IsNamespaceLevelPublicType(rawLine))
                return;
            if (currentNamespace.Length == 0)
                return;
            if (!TryExtractPublicTypeName(line, out ReadOnlySpan<byte> typeNameSpan))
                return;

            string typeName = CreateAsciiString(typeNameSpan);
            if (typeName.Length == 0)
                return;

            AppendPreservedType(writer, preservedTypes, currentNamespace + "." + typeName);
        }

        private static bool IsNamespaceLevelPublicType(ReadOnlySpan<byte> rawLine)
        {
            int leadingWhitespace = 0;
            while (leadingWhitespace < rawLine.Length &&
                   (rawLine[leadingWhitespace] == (byte)' ' || rawLine[leadingWhitespace] == (byte)'\t'))
                leadingWhitespace++;

            return leadingWhitespace <= 4;
        }

        private static bool TryExtractPublicTypeName(ReadOnlySpan<byte> line, out ReadOnlySpan<byte> typeName)
        {
            typeName = ReadOnlySpan<byte>.Empty;
            if (!StartsWithAscii(line, PublicToken))
                return false;

            ReadOnlySpan<byte> remainder = line.Slice(PublicToken.Length);
            if (StartsWithAscii(remainder, ReadonlyToken))
                remainder = remainder.Slice(ReadonlyToken.Length);
            if (StartsWithAscii(remainder, StaticToken))
                remainder = remainder.Slice(StaticToken.Length);

            int keywordLength;
            bool isDelegate = false;
            if (StartsWithAscii(remainder, InterfaceToken))
                keywordLength = InterfaceToken.Length;
            else if (StartsWithAscii(remainder, StructToken))
                keywordLength = StructToken.Length;
            else if (StartsWithAscii(remainder, EnumToken))
                keywordLength = EnumToken.Length;
            else if (StartsWithAscii(remainder, DelegateToken))
            {
                keywordLength = DelegateToken.Length;
                isDelegate = true;
            }
            else if (StartsWithAscii(remainder, ClassToken))
                keywordLength = ClassToken.Length;
            else
                return false;

            ReadOnlySpan<byte> nameAndTail = TrimStart(remainder.Slice(keywordLength));
            if (isDelegate)
            {
                int paren = nameAndTail.IndexOf((byte)'(');
                if (paren <= 0)
                    return false;

                int nameStart = -1;
                for (int i = paren - 1; i >= 0; i--)
                {
                    if (nameAndTail[i] == (byte)' ' || nameAndTail[i] == (byte)'\t')
                    {
                        nameStart = i;
                        break;
                    }
                }

                if (nameStart >= 0)
                    nameAndTail = nameAndTail.Slice(nameStart + 1);
            }

            int end = FindTypeNameEnd(nameAndTail);
            typeName = end > 0 ? nameAndTail.Slice(0, end) : nameAndTail;
            typeName = Trim(typeName);
            return typeName.Length > 0;
        }

        private static int FindTypeNameEnd(ReadOnlySpan<byte> nameAndTail)
        {
            for (int i = 0; i < nameAndTail.Length; i++)
            {
                byte c = nameAndTail[i];
                if (c == (byte)' ' ||
                    c == (byte)':' ||
                    c == (byte)'<' ||
                    c == (byte)'(' ||
                    c == (byte)'{')
                    return i;
            }

            return -1;
        }

        private static bool StartsWithAscii(ReadOnlySpan<byte> value, string token)
        {
            if (value.Length < token.Length)
                return false;

            for (int i = 0; i < token.Length; i++)
            {
                if (value[i] != (byte)token[i])
                    return false;
            }

            return true;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && (value[start] == (byte)' ' || value[start] == (byte)'\t'))
                start++;
            while (end >= start && (value[end] == (byte)' ' || value[end] == (byte)'\t'))
                end--;

            return start <= end ? value.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static ReadOnlySpan<byte> TrimStart(ReadOnlySpan<byte> value)
        {
            int start = 0;
            while (start < value.Length &&
                   (value[start] == (byte)' ' || value[start] == (byte)'\t'))
                start++;

            return start > 0 ? value.Slice(start) : value;
        }

        private static string CreateAsciiString(ReadOnlySpan<byte> value)
        {
            if (value.Length == 0 || value.Length > IdentifierScratch.Length)
                return string.Empty;

            for (int i = 0; i < value.Length; i++)
                IdentifierScratch[i] = (char)value[i];

            return new string(IdentifierScratch, 0, value.Length);
        }

        private static void AppendPreservedType(TextWriter writer, HashSet<string> preservedTypes, string fullName)
        {
            if (!preservedTypes.Add(fullName))
                return;

            writer.Write("    <type fullname=\"");
            writer.Write(fullName);
            writer.WriteLine("\" preserve=\"all\" />");
        }

        private static bool FilesEqual(string leftPath, string rightPath)
        {
            var leftInfo = new FileInfo(leftPath);
            var rightInfo = new FileInfo(rightPath);
            if (leftInfo.Length != rightInfo.Length)
                return false;

            using (FileStream left = File.OpenRead(leftPath))
            using (FileStream right = File.OpenRead(rightPath))
            {
                int leftByte;
                do
                {
                    leftByte = left.ReadByte();
                    if (leftByte != right.ReadByte())
                        return false;
                }
                while (leftByte != -1);
            }

            return true;
        }

        private static void WriteVaultOffsetsIfChanged()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string absolutePath = Path.Combine(projectRoot, VaultOffsetsPath.Replace('/', Path.DirectorySeparatorChar));
            string directory = Path.GetDirectoryName(absolutePath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string tempPath = absolutePath + ".tmp";
            using (var writer = new StreamWriter(tempPath, false, Encoding.ASCII))
            {
                WriteVaultOffsets(writer);
            }

            if (File.Exists(absolutePath) && FilesEqual(absolutePath, tempPath))
            {
                File.Delete(tempPath);
                return;
            }

            File.Copy(tempPath, absolutePath, true);
            File.Delete(tempPath);
        }

        private static void WriteVaultOffsets(TextWriter writer)
        {
            writer.WriteLine("// <auto-generated />");
            writer.WriteLine("namespace Hecton8.Global.Contracts.Generated");
            writer.WriteLine("{");
            writer.WriteLine("    /// <summary>");
            writer.WriteLine("    /// Static offsets for global compile-wall contracts. Regenerated by Compile Wall X-Ray tooling.");
            writer.WriteLine("    /// </summary>");
            writer.WriteLine("    public static class VaultOffsets");
            writer.WriteLine("    {");
            writer.WriteLine("        public const int GlobalSignalPayload_Size = 128;");
            writer.WriteLine("        public const int GlobalSignalPayload_TypeHash = 0;");
            writer.WriteLine("        public const int GlobalSignalPayload_PayloadBytes = 6;");
            writer.WriteLine("        public const int GlobalSignalPayload_PayloadStart = 16;");
            writer.WriteLine("        public const int GlobalNativeBufferHandle_Size = 32;");
            writer.WriteLine("        public const int GlobalNativeBufferHandle_Pointer = 0;");
            writer.WriteLine("        public const int GlobalNativeBufferHandle_Generation = 20;");
            writer.WriteLine("        public const int NativeMemoryAliasContract_Size = 64;");
            writer.WriteLine("        public const int AssemblyRoutingOverride_Size = 64;");
            writer.WriteLine("        public const int AssemblyRoutingOverride_MinQualityWeight = 20;");
            writer.WriteLine("        public const int AssemblyRoutingOverride_MaxQualityWeight = 24;");
            writer.WriteLine("        public const int AssemblyRoutingOverride_QualityCurveHash = 28;");
            writer.WriteLine("        public const int BootstrapRegistryContext_Size = 80;");
            writer.WriteLine("        public const int BootstrapRegistryContext_BufferTable = 8;");
            writer.WriteLine("        public const int BootstrapRegistryContext_RegistryTable = 40;");
            writer.WriteLine("        public const int BootstrapDependencySnapshot_Size = 80;");
            writer.WriteLine("        public const int BootstrapDependencySnapshot_ServiceTable = 8;");
            writer.WriteLine("        public const int BootstrapDependencySnapshot_SignalTable = 40;");
            writer.WriteLine("        public const int PhysicsFacade_Size = 40;");
            writer.WriteLine("        public const int MockDomainState_Size = 32;");
            writer.WriteLine("    }");
            writer.WriteLine("}");
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 32, Pack = 8)]
    internal struct AssemblyRoutingCsvRow
    {
        public uint ContractHash;
        public uint ImplementationHash;
        public uint MockImplementationHash;
        public uint Flags;
        public float MinQualityWeight;
        public float MaxQualityWeight;
        public uint QualityCurveHash;
        public uint Reserved;
    }

    internal static class AssemblyRoutingCsvOverride
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private const int MaxCsvBytes = 65536;
        private const string CsvPath = "Assets/_Project/Data/Config/assembly_routing.csv";
        private static readonly byte[] CsvScratch = new byte[MaxCsvBytes];

        public static bool TryReadFirstOverrideFromDisk(out AssemblyRoutingCsvRow row)
        {
            row = default;
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string absolutePath = Path.Combine(projectRoot, CsvPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolutePath))
                return false;

            using (var stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (stream.Length > CsvScratch.Length)
                    return false;

                int total = 0;
                while (total < stream.Length)
                {
                    int read = stream.Read(CsvScratch, total, (int)stream.Length - total);
                    if (read <= 0)
                        break;
                    total += read;
                }

                return TryParseFirstOverride(new ReadOnlySpan<byte>(CsvScratch, 0, total), out row);
            }
        }

        public static bool TryParseFirstOverride(ReadOnlySpan<byte> data, out AssemblyRoutingCsvRow row)
        {
            row = default;
            int lineStart = 0;
            bool skippedHeader = false;
            for (int i = 0; i <= data.Length; i++)
            {
                if (i < data.Length && data[i] != (byte)'\n')
                    continue;

                ReadOnlySpan<byte> line = Trim(data.Slice(lineStart, i - lineStart));
                lineStart = i + 1;
                if (line.Length == 0 || line[0] == (byte)'#')
                    continue;

                if (!skippedHeader && ContainsAlpha(line))
                {
                    skippedHeader = true;
                    continue;
                }

                return TryParseLine(line, out row);
            }

            return false;
        }

        private static bool TryParseLine(ReadOnlySpan<byte> line, out AssemblyRoutingCsvRow row)
        {
            row = default;
            ReadOnlySpan<byte> contract = NextToken(ref line);
            ReadOnlySpan<byte> implementation = NextToken(ref line);
            ReadOnlySpan<byte> mock = NextToken(ref line);
            ReadOnlySpan<byte> flags = NextToken(ref line);
            ReadOnlySpan<byte> minQualityWeight = NextToken(ref line);
            ReadOnlySpan<byte> maxQualityWeight = NextToken(ref line);
            ReadOnlySpan<byte> qualityCurve = NextToken(ref line);
            if (contract.Length == 0 || implementation.Length == 0 || mock.Length == 0)
                return false;

            row.ContractHash = Fnv1A(contract);
            row.ImplementationHash = Fnv1A(implementation);
            row.MockImplementationHash = Fnv1A(mock);
            row.Flags = ParseUInt(flags);
            row.MinQualityWeight = ParseWeight01(minQualityWeight, 0f);
            row.MaxQualityWeight = ParseWeight01(maxQualityWeight, 1f);
            if (row.MaxQualityWeight < row.MinQualityWeight)
                row.MaxQualityWeight = row.MinQualityWeight;
            row.QualityCurveHash = qualityCurve.Length > 0 ? Fnv1A(qualityCurve) : 0x5157414Cu;
            return true;
        }

        private static ReadOnlySpan<byte> NextToken(ref ReadOnlySpan<byte> line)
        {
            int comma = line.IndexOf((byte)',');
            if (comma < 0)
            {
                ReadOnlySpan<byte> last = Trim(line);
                line = ReadOnlySpan<byte>.Empty;
                return last;
            }

            ReadOnlySpan<byte> token = Trim(line.Slice(0, comma));
            line = line.Slice(comma + 1);
            return token;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && (value[start] == (byte)' ' || value[start] == (byte)'\r' || value[start] == (byte)'\t'))
                start++;
            while (end >= start && (value[end] == (byte)' ' || value[end] == (byte)'\r' || value[end] == (byte)'\t'))
                end--;
            return start <= end ? value.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool ContainsAlpha(ReadOnlySpan<byte> value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                byte b = value[i];
                if ((b >= (byte)'A' && b <= (byte)'Z') || (b >= (byte)'a' && b <= (byte)'z'))
                    return true;
            }

            return false;
        }

        private static uint Fnv1A(ReadOnlySpan<byte> value)
        {
            uint hash = FnvOffset;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= FnvPrime;
            }

            return hash;
        }

        private static uint ParseUInt(ReadOnlySpan<byte> value)
        {
            uint result = 0u;
            for (int i = 0; i < value.Length; i++)
            {
                byte b = value[i];
                if (b < (byte)'0' || b > (byte)'9')
                    break;
                result = result * 10u + (uint)(b - (byte)'0');
            }

            return result;
        }

        private static float ParseWeight01(ReadOnlySpan<byte> value, float fallback)
        {
            if (value.Length == 0)
                return fallback;

            int index = 0;
            uint whole = 0u;
            uint fraction = 0u;
            uint divisor = 1u;
            bool sawDigit = false;
            while (index < value.Length)
            {
                byte b = value[index];
                if (b < (byte)'0' || b > (byte)'9')
                    break;

                sawDigit = true;
                whole = whole * 10u + (uint)(b - (byte)'0');
                index++;
            }

            if (index < value.Length && value[index] == (byte)'.')
            {
                index++;
                while (index < value.Length)
                {
                    byte b = value[index];
                    if (b < (byte)'0' || b > (byte)'9')
                        break;

                    sawDigit = true;
                    if (divisor < 1000000u)
                    {
                        fraction = fraction * 10u + (uint)(b - (byte)'0');
                        divisor *= 10u;
                    }

                    index++;
                }
            }

            if (!sawDigit)
                return fallback;

            float result = whole + fraction / (float)divisor;
            if (result < 0f)
                return 0f;
            return result > 1f ? 1f : result;
        }
    }

    public sealed class CompileWallXRayWindow : EditorWindow
    {
        private readonly CompileWallCompilationSample[] _samples = new CompileWallCompilationSample[300];
        private Vector2 _scroll;
        private CompileWallGraphScan _scan;

        [MenuItem("Tools/Hecton-8/Compile Wall X-Ray")]
        public static void Open()
        {
            GetWindow<CompileWallXRayWindow>("Compile Wall X-Ray");
        }

        private void OnEnable()
        {
            RefreshScan();
        }

        private void OnGUI()
        {
            DrawToolbar();
            if (_scan == null)
                RefreshScan();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Asmdef count", _scan.Nodes.Count);
                EditorGUILayout.IntField("Runtime -> Runtime illegal edges", _scan.IllegalRuntimeEdges.Count);
                EditorGUILayout.IntField("Cycle assemblies", _scan.CycleAssemblies.Count);
                EditorGUILayout.IntField("Domain health rows", _scan.DomainHealth.Count);
                EditorGUILayout.IntField("Stale ref artifacts", _scan.ArtifactSkews.Count);
                EditorGUILayout.IntField("ARM64 Pack=1 hits", _scan.PackViolationTotal);
                EditorGUILayout.IntField("Legacy graph headers", _scan.LegacyGraphHeaders.Count);
                EditorGUILayout.DoubleField("Scan ms", _scan.ScanMilliseconds);
                EditorGUILayout.Toggle("Asmdef graph acyclic", _scan.CycleAssemblies.Count == 0);
                EditorGUILayout.Toggle("Emergency mock atlas", _scan.UsedEmergencyMockAtlas);
            }

            Rect graphRect = GUILayoutUtility.GetRect(Mathf.Max(position.width - 20f, 320f), 280f);
            DrawGraph(graphRect);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawDomainHealth();
            DrawIllegalEdges();
            DrawCycleAssemblies();
            DrawArtifactSkews();
            DrawPackViolations();
            DrawCompilationSamples();
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Rescan", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                RefreshScan();
            if (GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                ValidateNow();
            if (GUILayout.Button("Generate link.xml", EditorStyles.toolbarButton, GUILayout.Width(120f)))
                CompileWallGeneratedArtifacts.GenerateAll();
            if (GUILayout.Button("Nuke Domain Reload", EditorStyles.toolbarButton, GUILayout.Width(150f)))
                NukeDomainReload();
            EditorGUILayout.EndHorizontal();
        }

        private void RefreshScan()
        {
            _scan = CompileWallAssemblyGraphScanner.ScanProject();
            Repaint();
        }

        private void ValidateNow()
        {
            RefreshScan();
            if (_scan.IllegalRuntimeEdges.Count == 0 && _scan.CycleAssemblies.Count == 0)
            {
                Debug.Log("[CompileWall] Asmdef graph passed Runtime -> Runtime and cycle guards.");
                return;
            }

            if (_scan.IllegalRuntimeEdges.Count > 0)
                Debug.LogError("[CompileWall] Runtime -> Runtime illegal edges detected. Open Compile Wall X-Ray for count and paths.");
            if (_scan.CycleAssemblies.Count > 0)
                Debug.LogError("[CompileWall] Asmdef dependency cycles detected. Open Compile Wall X-Ray for count and paths.");
        }

        private static void NukeDomainReload()
        {
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
            Debug.LogWarning("[CompileWall] Enter Play Mode now skips Domain Reload and Scene Reload. Static reset nodes must clear their own state.");
        }

        private void DrawGraph(Rect rect)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            GUI.Box(rect, GUIContent.none);
            var nodeRects = new Dictionary<CompileWallAsmdefNode, Rect>(_scan.Nodes.Count);
            int[] layerCounts = new int[6];
            for (int i = 0; i < _scan.Nodes.Count; i++)
            {
                CompileWallAsmdefNode node = _scan.Nodes[i];
                int layer = Mathf.Clamp((int)node.Layer, 0, 5);
                int row = layerCounts[layer]++;
                float x = rect.x + 12f + layer * ((rect.width - 24f) / 6f);
                float y = rect.y + 24f + (row % 12) * 20f;
                var nodeRect = new Rect(x, y, Mathf.Max(70f, (rect.width - 60f) / 7f), 16f);
                nodeRects[node] = nodeRect;
                GUI.Label(nodeRect, node.DisplayName, EditorStyles.miniLabel);
            }

            Handles.BeginGUI();
            for (int i = 0; i < _scan.Edges.Count; i++)
            {
                CompileWallAsmdefEdge edge = _scan.Edges[i];
                if (!nodeRects.TryGetValue(edge.From, out Rect fromRect) || !nodeRects.TryGetValue(edge.To, out Rect toRect))
                    continue;

                Vector3 from = new Vector3(fromRect.xMax, fromRect.center.y, 0f);
                Vector3 to = new Vector3(toRect.xMin, toRect.center.y, 0f);
                if (edge.IllegalRuntimeEdge)
                {
                    Handles.color = new Color(1f, 0f, 0f, 0.85f);
                    Handles.DrawAAPolyLine(6f, from, to);
                    Handles.color = new Color(1f, 0.8f, 0.2f, 0.95f);
                    Handles.DrawAAPolyLine(2f, from, to);
                }
                else
                {
                    Handles.color = new Color(0.25f, 0.55f, 0.7f, 0.18f);
                    Handles.DrawAAPolyLine(1f, from, to);
                }
            }

            Handles.EndGUI();
        }

        private void DrawDomainHealth()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Domain Assembly Health", EditorStyles.boldLabel);
            int count = _scan.DomainHealth.Count;
            if (count == 0)
            {
                EditorGUILayout.LabelField("none");
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Domain", GUILayout.MinWidth(160f));
                EditorGUILayout.LabelField("Assemblies", GUILayout.Width(80f));
                EditorGUILayout.LabelField("Runtime", GUILayout.Width(70f));
                EditorGUILayout.LabelField("Illegal", GUILayout.Width(70f));
                EditorGUILayout.LabelField("Cycles", GUILayout.Width(70f));
                EditorGUILayout.LabelField("State", GUILayout.Width(70f));
                EditorGUILayout.EndHorizontal();
            }

            int limit = count < CompileWallGraphScan.MaxStoredDomainHealthRows
                ? count
                : CompileWallGraphScan.MaxStoredDomainHealthRows;
            for (int i = 0; i < limit; i++)
            {
                CompileWallDomainHealth health = _scan.DomainHealth[i];
                bool pass = health.IllegalRuntimeEdges == 0 && health.CycleAssemblies == 0;
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(health.DomainName, GUILayout.MinWidth(160f));
                    EditorGUILayout.IntField(health.AssemblyCount, GUILayout.Width(80f));
                    EditorGUILayout.IntField(health.RuntimeAssemblyCount, GUILayout.Width(70f));
                    EditorGUILayout.IntField(health.IllegalRuntimeEdges, GUILayout.Width(70f));
                    EditorGUILayout.IntField(health.CycleAssemblies, GUILayout.Width(70f));
                    EditorGUILayout.LabelField(pass ? "PASS" : "FAIL", GUILayout.Width(70f));
                    EditorGUILayout.EndHorizontal();
                }
            }

            if (count > limit)
                EditorGUILayout.LabelField("domain list truncated");
        }

        private void DrawIllegalEdges()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Illegal Runtime -> Runtime Edges", EditorStyles.boldLabel);
            int count = _scan.IllegalRuntimeEdges.Count;
            if (count == 0)
            {
                EditorGUILayout.LabelField("none");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                CompileWallAsmdefEdge edge = _scan.IllegalRuntimeEdges[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(edge.From.Name, GUILayout.MinWidth(180f));
                EditorGUILayout.LabelField("->", GUILayout.Width(22f));
                EditorGUILayout.LabelField(edge.To.Name, GUILayout.MinWidth(180f));
                EditorGUILayout.LabelField(edge.From.Path);
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawCycleAssemblies()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Asmdef Dependency Cycles", EditorStyles.boldLabel);
            int count = _scan.CycleAssemblies.Count;
            if (count == 0)
            {
                EditorGUILayout.LabelField("PASS: graph is acyclic");
                return;
            }

            int limit = count < CompileWallGraphScan.MaxStoredCycleAssemblies
                ? count
                : CompileWallGraphScan.MaxStoredCycleAssemblies;
            for (int i = 0; i < limit; i++)
            {
                string assemblyName = _scan.CycleAssemblies[i];
                CompileWallAsmdefNode node = _scan.FindNodeByName(assemblyName);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(assemblyName, GUILayout.MinWidth(180f));
                EditorGUILayout.LabelField(node != null ? node.Path : string.Empty);
                EditorGUILayout.EndHorizontal();
            }
            if (count > limit)
                EditorGUILayout.LabelField("cycle list truncated");
        }

        private void DrawArtifactSkews()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Stale Contract Ref Artifacts", EditorStyles.boldLabel);
            int count = _scan.ArtifactSkews.Count;
            if (count == 0)
            {
                EditorGUILayout.LabelField("none");
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Assembly", GUILayout.MinWidth(180f));
                EditorGUILayout.LabelField("Contract", GUILayout.MinWidth(180f));
                EditorGUILayout.LabelField("Seconds", GUILayout.Width(90f));
                EditorGUILayout.LabelField("Ref artifact");
                EditorGUILayout.EndHorizontal();
            }

            for (int i = 0; i < count; i++)
            {
                CompileWallArtifactSkew skew = _scan.ArtifactSkews[i];
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(skew.From.Name, GUILayout.MinWidth(180f));
                    EditorGUILayout.LabelField(skew.To.Name, GUILayout.MinWidth(180f));
                    EditorGUILayout.DoubleField(skew.SecondsBehind, GUILayout.Width(90f));
                    EditorGUILayout.LabelField(skew.FromRefPath);
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        private void DrawPackViolations()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Runtime Struct Pack=1 Violations", EditorStyles.boldLabel);
            int totalCount = _scan.PackViolationTotal;
            int storedCount = _scan.PackViolations.Count;
            if (totalCount == 0)
            {
                EditorGUILayout.LabelField("none");
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Path", GUILayout.MinWidth(260f));
                EditorGUILayout.LabelField("Line", GUILayout.Width(60f));
                EditorGUILayout.LabelField("Directive");
                EditorGUILayout.EndHorizontal();
            }

            int limit = storedCount < 160 ? storedCount : 160;
            for (int i = 0; i < limit; i++)
            {
                CompileWallPackViolation violation = _scan.PackViolations[i];
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(violation.Path, GUILayout.MinWidth(260f));
                    EditorGUILayout.IntField(violation.LineNumber, GUILayout.Width(60f));
                    EditorGUILayout.LabelField(violation.Snippet);
                    EditorGUILayout.EndHorizontal();
                }
            }

            if (totalCount > limit)
                EditorGUILayout.IntField("Hidden Pack=1 hits", totalCount - limit);
        }

        private void DrawCompilationSamples()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Compilation Samples", EditorStyles.boldLabel);
            int count = CompileWallTelemetryRecorder.CopySamples(_samples);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Assembly", GUILayout.MinWidth(220f));
                EditorGUILayout.LabelField("Seconds", GUILayout.Width(90f));
                EditorGUILayout.LabelField("Warnings", GUILayout.Width(70f));
                EditorGUILayout.LabelField("Errors", GUILayout.Width(70f));
                EditorGUILayout.EndHorizontal();
            }

            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrEmpty(_samples[i].AssemblyLabel))
                    continue;

                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(_samples[i].AssemblyLabel, GUILayout.MinWidth(220f));
                    EditorGUILayout.DoubleField(_samples[i].Seconds, GUILayout.Width(90f));
                    EditorGUILayout.IntField(_samples[i].WarningCount, GUILayout.Width(70f));
                    EditorGUILayout.IntField(_samples[i].ErrorCount, GUILayout.Width(70f));
                    EditorGUILayout.EndHorizontal();
                }
            }
        }
    }
}
