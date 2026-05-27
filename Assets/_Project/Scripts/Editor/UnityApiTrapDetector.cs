#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Static editor scan for Unity APIs that allocate hidden managed arrays.
    /// </summary>
    [InitializeOnLoad]
    internal static class UnityApiTrapDetector
    {
        private const string SourceRoot = "Assets/_Project/Scripts";
        private const int MaxConsoleReports = 32;

        private static readonly TrapRule[] _rules =
        {
            new TrapRule(TrapRuleKind.Literal, "Input.touches", "Input.touches allocates. Use Input.GetTouch(index) with touchCount."),
            new TrapRule(TrapRuleKind.Literal, "Resources.Load", "Resources.Load is forbidden in runtime code. Use Addressables or a serialized owner registration route."),
            new TrapRule(TrapRuleKind.RendererSharedMaterialsGetter, "sharedMaterials", "Renderer.sharedMaterials returns a copied array. Use Renderer.GetSharedMaterials(cachedList)."),
            new TrapRule(TrapRuleKind.RendererMaterial, "materials", "Renderer.materials allocates and instantiates materials. Use sharedMaterials/GetSharedMaterials or an explicit owned material lane."),
            new TrapRule(TrapRuleKind.RendererMaterial, "material", "Renderer.material leaks/clones. Use sharedMaterial or an explicit owned material lane."),
            new TrapRule(TrapRuleKind.MeshVerticesGetter, "vertices", "Mesh.vertices getter allocates a copy. Use Mesh.GetVertices(cachedList) or AcquireReadOnlyMeshData."),
            new TrapRule(TrapRuleKind.GenericGetComponentsArray, "GetComponents", "Generic GetComponents array overload allocates. Use a cached List<T> overload.")
        };

        static UnityApiTrapDetector()
        {
            EditorApplication.delayCall -= ScanAfterReload;
            EditorApplication.delayCall += ScanAfterReload;
        }

        [MenuItem("Hecton-8/Compliance/Scan Unity API Traps")]
        private static void ScanFromMenu()
        {
            int violations = Scan(reportToConsole: true);
            SessionState.SetInt("UnityApiTrapDetector.Violations", violations);
        }

        private static void ScanAfterReload()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall -= ScanAfterReload;
                EditorApplication.delayCall += ScanAfterReload;
                return;
            }

            SessionState.SetInt("UnityApiTrapDetector.Violations", Scan(reportToConsole: false));
        }

        private static int Scan(bool reportToConsole)
        {
            if (!Directory.Exists(SourceRoot))
                return 0;

            List<string> paths = new List<string>(Directory.EnumerateFiles(SourceRoot, "*.cs", SearchOption.AllDirectories));
            paths.Sort(StringComparer.Ordinal);

            int violations = 0;
            int reported = 0;
            for (int pathIndex = 0; pathIndex < paths.Count; pathIndex++)
            {
                string path = paths[pathIndex];
                if (!IsRuntimeScriptPath(path))
                    continue;

                string[] lines = ReadAllLinesSafe(path);
                Dictionary<string, string> nonRendererMaterialSymbols = BuildNonRendererMaterialSymbolIndex(lines);
                List<bool> editorOnlyPreprocessorStack = new List<bool>(4);
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    string rawLine = lines[lineIndex];
                    if (TryConsumePreprocessorDirective(rawLine, editorOnlyPreprocessorStack))
                        continue;

                    if (IsInsideEditorOnlyPreprocessorBlock(editorOnlyPreprocessorStack))
                        continue;

                    bool hasColdAllocWaiver = rawLine.IndexOf("COLD ALLOC:", StringComparison.Ordinal) >= 0;
                    string codeLine = StripLineComment(StripStringLiterals(rawLine));
                    for (int ruleIndex = 0; ruleIndex < _rules.Length; ruleIndex++)
                    {
                        TrapRule rule = _rules[ruleIndex];
                        if (!IsTrapHit(codeLine, rule, nonRendererMaterialSymbols, hasColdAllocWaiver))
                            continue;

                        violations++;
                        if (reportToConsole && reported < MaxConsoleReports)
                        {
                            Debug.LogError(
                                "[UnityApiTrapDetector] " +
                                path +
                                ":" +
                                (lineIndex + 1) +
                                " " +
                                rule.Message);
                            reported++;
                        }
                    }
                }
            }

            return violations;
        }

        private static bool IsRuntimeScriptPath(string path)
        {
            string normalized = path.Replace('\\', '/');
            return normalized.StartsWith(SourceRoot + "/", StringComparison.Ordinal) &&
                   normalized.IndexOf("/Editor/", StringComparison.Ordinal) < 0;
        }

        private static string[] ReadAllLinesSafe(string path)
        {
            try
            {
                return File.ReadAllLines(path);
            }
            catch (IOException)
            {
                return Array.Empty<string>();
            }
            catch (UnauthorizedAccessException)
            {
                return Array.Empty<string>();
            }
        }

        private static string StripLineComment(string line)
        {
            int commentIndex = line.IndexOf("//", StringComparison.Ordinal);
            return commentIndex >= 0 ? line.Substring(0, commentIndex) : line;
        }

        private static string StripStringLiterals(string line)
        {
            if (string.IsNullOrEmpty(line) || line.IndexOf('"') < 0)
                return line;

            char[] chars = line.ToCharArray();
            bool inString = false;
            bool verbatim = false;
            for (int i = 0; i < chars.Length; i++)
            {
                char value = chars[i];
                if (!inString)
                {
                    if (value != '"')
                        continue;

                    inString = true;
                    verbatim = HasVerbatimStringPrefix(chars, i);
                    chars[i] = ' ';
                    continue;
                }

                chars[i] = ' ';
                if (verbatim)
                {
                    if (value != '"')
                        continue;

                    if (i + 1 < chars.Length && chars[i + 1] == '"')
                    {
                        chars[i + 1] = ' ';
                        i++;
                        continue;
                    }

                    inString = false;
                    verbatim = false;
                    continue;
                }

                if (value == '\\' && i + 1 < chars.Length)
                {
                    chars[i + 1] = ' ';
                    i++;
                    continue;
                }

                if (value == '"')
                    inString = false;
            }

            return new string(chars);
        }

        private static bool HasVerbatimStringPrefix(char[] chars, int quoteIndex)
        {
            bool hasAt = false;
            for (int i = quoteIndex - 1; i >= 0 && (chars[i] == '@' || chars[i] == '$'); i--)
            {
                if (chars[i] == '@')
                    hasAt = true;
            }

            return hasAt;
        }

        private static bool IsTrapHit(
            string codeLine,
            TrapRule rule,
            Dictionary<string, string> nonRendererMaterialSymbols,
            bool hasColdAllocWaiver)
        {
            switch (rule.Kind)
            {
                case TrapRuleKind.Literal:
                    return codeLine.IndexOf(rule.MemberName, StringComparison.Ordinal) >= 0;
                case TrapRuleKind.RendererMaterial:
                    return !hasColdAllocWaiver &&
                           IsRendererMaterialTrapHit(codeLine, rule.MemberName, nonRendererMaterialSymbols);
                case TrapRuleKind.RendererSharedMaterialsGetter:
                    return IsRendererSharedMaterialsGetterTrapHit(codeLine);
                case TrapRuleKind.MeshVerticesGetter:
                    return IsMeshVerticesGetterTrapHit(codeLine);
                case TrapRuleKind.GenericGetComponentsArray:
                    return IsGenericGetComponentsArrayTrapHit(codeLine);
                default:
                    return false;
            }
        }

        private static bool IsRendererMaterialTrapHit(
            string codeLine,
            string memberName,
            Dictionary<string, string> nonRendererMaterialSymbols)
        {
            if (codeLine.IndexOf(".sharedMaterial", StringComparison.Ordinal) >= 0 ||
                codeLine.IndexOf(".sharedMaterials", StringComparison.Ordinal) >= 0)
            {
                return false;
            }

            int searchIndex = 0;
            while (TryFindExactMemberAccess(codeLine, memberName, searchIndex, out int memberStart, out int memberEnd))
            {
                searchIndex = memberEnd;
                string receiver = ExtractReceiverIdentifier(codeLine, memberStart - 1);
                if (!IsKnownNonRendererMaterialSymbol(receiver, nonRendererMaterialSymbols) &&
                    !IsKnownNonRendererMaterialReceiver(receiver) &&
                    !IsKnownNonRendererMaterialLine(codeLine))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryConsumePreprocessorDirective(string rawLine, List<bool> editorOnlyPreprocessorStack)
        {
            string trimmed = rawLine.TrimStart();
            if (trimmed.Length == 0 || trimmed[0] != '#')
                return false;

            if (trimmed.StartsWith("#if", StringComparison.Ordinal))
            {
                bool parentEditorOnly = IsInsideEditorOnlyPreprocessorBlock(editorOnlyPreprocessorStack);
                editorOnlyPreprocessorStack.Add(parentEditorOnly || IsUnityEditorOnlyExpression(trimmed));
                return true;
            }

            if (trimmed.StartsWith("#elif", StringComparison.Ordinal))
            {
                if (editorOnlyPreprocessorStack.Count > 0)
                {
                    bool parentEditorOnly = IsInsideEditorOnlyPreprocessorBlock(editorOnlyPreprocessorStack, editorOnlyPreprocessorStack.Count - 1);
                    editorOnlyPreprocessorStack[editorOnlyPreprocessorStack.Count - 1] = parentEditorOnly || IsUnityEditorOnlyExpression(trimmed);
                }

                return true;
            }

            if (trimmed.StartsWith("#else", StringComparison.Ordinal))
            {
                if (editorOnlyPreprocessorStack.Count > 0)
                {
                    bool parentEditorOnly = IsInsideEditorOnlyPreprocessorBlock(editorOnlyPreprocessorStack, editorOnlyPreprocessorStack.Count - 1);
                    editorOnlyPreprocessorStack[editorOnlyPreprocessorStack.Count - 1] = parentEditorOnly;
                }

                return true;
            }

            if (trimmed.StartsWith("#endif", StringComparison.Ordinal))
            {
                if (editorOnlyPreprocessorStack.Count > 0)
                    editorOnlyPreprocessorStack.RemoveAt(editorOnlyPreprocessorStack.Count - 1);

                return true;
            }

            return true;
        }

        private static bool IsUnityEditorOnlyExpression(string directive)
        {
            return directive.IndexOf("UNITY_EDITOR", StringComparison.Ordinal) >= 0 &&
                   directive.IndexOf("!UNITY_EDITOR", StringComparison.Ordinal) < 0 &&
                   directive.IndexOf("!defined(UNITY_EDITOR)", StringComparison.Ordinal) < 0;
        }

        private static bool IsInsideEditorOnlyPreprocessorBlock(List<bool> editorOnlyPreprocessorStack)
        {
            return IsInsideEditorOnlyPreprocessorBlock(editorOnlyPreprocessorStack, editorOnlyPreprocessorStack.Count);
        }

        private static bool IsInsideEditorOnlyPreprocessorBlock(List<bool> editorOnlyPreprocessorStack, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (editorOnlyPreprocessorStack[i])
                    return true;
            }

            return false;
        }

        private static bool IsRendererSharedMaterialsGetterTrapHit(string codeLine)
        {
            int searchIndex = 0;
            while (TryFindExactMemberAccess(codeLine, "sharedMaterials", searchIndex, out int memberStart, out int memberEnd))
            {
                searchIndex = memberEnd;

                int cursor = memberEnd;
                while (cursor < codeLine.Length && char.IsWhiteSpace(codeLine[cursor]))
                    cursor++;

                if (cursor < codeLine.Length && codeLine[cursor] == '=')
                    continue;

                return true;
            }

            return false;
        }

        private static bool IsMeshVerticesGetterTrapHit(string codeLine)
        {
            int searchIndex = 0;
            while (TryFindExactMemberAccess(codeLine, "vertices", searchIndex, out int memberStart, out int memberEnd))
            {
                searchIndex = memberEnd;
                string receiver = ExtractReceiverIdentifier(codeLine, memberStart - 1);
                if (string.Equals(receiver, "meshInfo", StringComparison.Ordinal))
                    continue;

                int cursor = memberEnd;
                while (cursor < codeLine.Length && char.IsWhiteSpace(codeLine[cursor]))
                    cursor++;

                if (cursor < codeLine.Length && codeLine[cursor] == '=')
                    continue;

                return true;
            }

            return false;
        }

        private static bool IsGenericGetComponentsArrayTrapHit(string codeLine)
        {
            return IsGenericGetComponentsArrayTrapHit(codeLine, "GetComponents") ||
                   IsGenericGetComponentsArrayTrapHit(codeLine, "GetComponentsInChildren") ||
                   IsGenericGetComponentsArrayTrapHit(codeLine, "GetComponentsInParent");
        }

        private static bool IsGenericGetComponentsArrayTrapHit(string codeLine, string methodName)
        {
            string needle = methodName + "<";
            int searchIndex = 0;
            while (true)
            {
                int methodStart = codeLine.IndexOf(needle, searchIndex, StringComparison.Ordinal);
                if (methodStart < 0)
                    return false;

                searchIndex = methodStart + needle.Length;
                int openParen = codeLine.IndexOf('(', searchIndex);
                if (openParen < 0)
                    return false;

                int closeParen = codeLine.IndexOf(')', openParen + 1);
                if (closeParen < 0)
                    return false;

                string args = codeLine.Substring(openParen + 1, closeParen - openParen - 1).Trim();
                if (string.Equals(methodName, "GetComponents", StringComparison.Ordinal))
                {
                    if (args.Length == 0)
                        return true;

                    continue;
                }

                if (args.Length == 0 ||
                    string.Equals(args, "true", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(args, "false", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        private static bool TryFindExactMemberAccess(
            string codeLine,
            string memberName,
            int startIndex,
            out int memberStart,
            out int memberEnd)
        {
            string needle = "." + memberName;
            int index = codeLine.IndexOf(needle, startIndex, StringComparison.Ordinal);
            while (index >= 0)
            {
                memberStart = index + 1;
                memberEnd = memberStart + memberName.Length;
                if ((memberEnd >= codeLine.Length || !IsIdentifierPart(codeLine[memberEnd])) &&
                    index > 0 &&
                    IsIdentifierPart(codeLine[index - 1]))
                {
                    return true;
                }

                index = codeLine.IndexOf(needle, index + needle.Length, StringComparison.Ordinal);
            }

            memberStart = -1;
            memberEnd = -1;
            return false;
        }

        private static string ExtractReceiverIdentifier(string codeLine, int dotIndex)
        {
            int cursor = dotIndex - 1;
            while (cursor >= 0 && char.IsWhiteSpace(codeLine[cursor]))
                cursor--;

            int end = cursor + 1;
            while (cursor >= 0 && IsIdentifierPart(codeLine[cursor]))
                cursor--;

            return end > cursor + 1 ? codeLine.Substring(cursor + 1, end - cursor - 1) : string.Empty;
        }

        private static bool IsIdentifierPart(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_';
        }

        private static bool IsKnownNonRendererMaterialReceiver(string receiver)
        {
            return receiver.IndexOf("font", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   receiver.IndexOf("graphic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   receiver.IndexOf("image", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   receiver.IndexOf("label", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   receiver.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   receiver.IndexOf("passData", StringComparison.Ordinal) >= 0 ||
                   string.Equals(receiver, "data", StringComparison.Ordinal);
        }

        private static bool IsKnownNonRendererMaterialSymbol(
            string receiver,
            Dictionary<string, string> nonRendererMaterialSymbols)
        {
            return !string.IsNullOrEmpty(receiver) &&
                   nonRendererMaterialSymbols != null &&
                   nonRendererMaterialSymbols.ContainsKey(receiver);
        }

        private static bool IsKnownNonRendererMaterialLine(string codeLine)
        {
            return codeLine.IndexOf("materialForRendering", StringComparison.Ordinal) >= 0 ||
                   codeLine.IndexOf("fontSharedMaterial", StringComparison.Ordinal) >= 0 ||
                   codeLine.IndexOf("materialReferenceIndex", StringComparison.Ordinal) >= 0 ||
                   codeLine.IndexOf("TMP_", StringComparison.Ordinal) >= 0 ||
                   codeLine.IndexOf("ShaderUtilities.", StringComparison.Ordinal) >= 0;
        }

        private static Dictionary<string, string> BuildNonRendererMaterialSymbolIndex(string[] lines)
        {
            Dictionary<string, string> symbols = new Dictionary<string, string>(StringComparer.Ordinal);
            if (lines == null)
                return symbols;

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string codeLine = StripLineComment(lines[lineIndex]);
                IndexSymbolsOfType(codeLine, "Graphic", symbols);
                IndexSymbolsOfType(codeLine, "MaskableGraphic", symbols);
                IndexSymbolsOfType(codeLine, "Image", symbols);
                IndexSymbolsOfType(codeLine, "RawImage", symbols);
                IndexSymbolsOfType(codeLine, "TMP_Text", symbols);
                IndexSymbolsOfType(codeLine, "TextMeshProUGUI", symbols);
                IndexSymbolsOfType(codeLine, "TMP_FontAsset", symbols);
            }

            return symbols;
        }

        private static void IndexSymbolsOfType(string codeLine, string typeName, Dictionary<string, string> symbols)
        {
            int searchIndex = 0;
            while (searchIndex < codeLine.Length)
            {
                int typeIndex = codeLine.IndexOf(typeName, searchIndex, StringComparison.Ordinal);
                if (typeIndex < 0)
                    return;

                int typeEnd = typeIndex + typeName.Length;
                bool hasLeftBoundary = typeIndex == 0 || !IsIdentifierPart(codeLine[typeIndex - 1]);
                bool hasRightBoundary = typeEnd >= codeLine.Length || !IsIdentifierPart(codeLine[typeEnd]);
                searchIndex = typeEnd;
                if (!hasLeftBoundary || !hasRightBoundary)
                    continue;

                int cursor = typeEnd;
                while (cursor < codeLine.Length && char.IsWhiteSpace(codeLine[cursor]))
                    cursor++;

                if (cursor >= codeLine.Length || !IsIdentifierStart(codeLine[cursor]))
                    continue;

                int identifierStart = cursor;
                cursor++;
                while (cursor < codeLine.Length && IsIdentifierPart(codeLine[cursor]))
                    cursor++;

                string symbol = codeLine.Substring(identifierStart, cursor - identifierStart);
                if (!symbols.ContainsKey(symbol))
                    symbols.Add(symbol, typeName);
            }
        }

        private static bool IsIdentifierStart(char value)
        {
            return char.IsLetter(value) || value == '_';
        }

        private readonly struct TrapRule
        {
            public readonly TrapRuleKind Kind;
            public readonly string MemberName;
            public readonly string Message;

            public TrapRule(TrapRuleKind kind, string memberName, string message)
            {
                Kind = kind;
                MemberName = memberName;
                Message = message;
            }
        }

        private enum TrapRuleKind
        {
            Literal,
            RendererSharedMaterialsGetter,
            RendererMaterial,
            MeshVerticesGetter,
            GenericGetComponentsArray
        }
    }
}
#endif
