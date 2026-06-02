using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Bakers
{
    public readonly struct ApexVerificationResult1605
    {
        public readonly int SourceFileCount;
        public readonly int HotMethodCount;
        public readonly int DataVaultTokenCount;
        public readonly int ViolationCount;
        public readonly string FirstViolation;

        public ApexVerificationResult1605(int sourceFileCount, int hotMethodCount, int dataVaultTokenCount, int violationCount, string firstViolation)
        {
            SourceFileCount = sourceFileCount;
            HotMethodCount = hotMethodCount;
            DataVaultTokenCount = dataVaultTokenCount;
            ViolationCount = violationCount;
            FirstViolation = firstViolation ?? string.Empty;
        }
    }

    public static class ApexIntegratorVerifier1605
    {
        private const string BakerSourceRoot = "Assets/_Project/Editor/Bakers";
        private const string VerifierSourceFileName = "/ApexIntegratorVerifier1605.cs";

        private static readonly string[] s_hotMethodNames =
        {
            "Tick",
            "FixedTick",
            "LateFrameTick",
            "Update",
            "FixedUpdate",
            "LateUpdate",
            "Execute"
        };

        private static readonly string[] s_hotForbiddenTokens =
        {
            "GlobalRegistry.Get<",
            ".GetComponent<",
            "GetComponent<",
            ".GetComponent(",
            "GetComponent(",
            "GlobalDataVault.TryGetLatestCreated",
            "TryGetLatestCreated(",
            "Resources.Load",
            "FindObjectOfType",
            "GameObject.Find"
        };

        private static readonly string[] s_runtimePhaseTokens =
        {
            "void Update(",
            "void FixedUpdate(",
            "void LateUpdate(",
            "Tick(float",
            "FixedTick(float",
            "LateFrameTick",
            "VISUAL_SYNC",
            "SystemDispatcher.",
            "GlobalRegistry.Get<"
        };

        private static readonly string[] s_dataVaultTokens =
        {
            "GlobalDataVault",
            "TryGetLatestCreated(",
            "AcquireWriteLock",
            "TryAcquireWriteLock",
            "ReleaseWriteLock",
            "WriteLock"
        };

        private static readonly string[] s_writeLockAcquireTokens =
        {
            "AcquireWriteLock",
            "TryAcquireWriteLock"
        };

        private static readonly string[] s_buildSpawnTokens =
        {
            "dotnet build",
            "BuildPipeline.BuildPlayer",
            "System.Diagnostics.Process.Start",
            "Process.Start(",
            "ProcessStartInfo"
        };

        private static readonly string[] s_requiredMemoryCeilingTokens =
        {
            "MaxEncodedPngBytes",
            "MaxRollbackAssetBytes",
            "MaxRollbackMetaBytes",
            "TryWriteBytesAtomicAbsolute",
            "MaxTextureSetsPerAtlas",
            "MaxAtlasScratchBytes",
            "MaxAtlasSourcePixels",
            "MaxAtlasEncodedPngBytes",
            "MaxMeshUvRollbackBytes"
        };

        private static readonly string[] s_requiredTransactionSafetyTokens =
        {
            "TryResolveComputeKernel",
            "TryFindPackedRectForSource",
            "IsRecoverableEditorException",
            "inputs.Length == 0",
            "TryRestoreMeshUvRollbackSnapshots",
            "TryRestoreTextureReadableState",
            "TryCaptureAssetFileRollbackSnapshots(albedoPath, normalPath, maskPath, materialPath"
        };

        [MenuItem("HECTON-8/Bakers/1605/Run Apex Source Verification", false, 207)]
        public static void RunMenuVerification()
        {
            if (!RunSourceVerification(out ApexVerificationResult1605 result))
            {
                Debug.LogError("[Apex1605] Source verification failed: " + result.FirstViolation);
                return;
            }

            Debug.Log("[Apex1605] Source verification passed. Files=" + result.SourceFileCount + " HotMethods=" + result.HotMethodCount);
        }

        public static bool RunSourceVerification(out ApexVerificationResult1605 result)
        {
            return RunSourceVerification(BakerSourceRoot, out result);
        }

        public static bool RunSourceVerification(string sourceRoot, out ApexVerificationResult1605 result)
        {
            result = default;
            if (string.IsNullOrEmpty(sourceRoot) || !Directory.Exists(sourceRoot))
            {
                result = new ApexVerificationResult1605(0, 0, 0, 1, "source root missing: " + sourceRoot);
                return false;
            }

            if (!TryCollectSourceFiles(sourceRoot, out string[] files, out string fileFailure))
            {
                result = new ApexVerificationResult1605(0, 0, 0, 1, fileFailure);
                return false;
            }

            int hotMethodCount = 0;
            int dataVaultTokenCount = 0;

            for (int i = 0; i < files.Length; i++)
            {
                string file = NormalizePath(files[i]);
                if (!TryReadSourceFile(file, out string source, out string readFailure))
                {
                    result = new ApexVerificationResult1605(files.Length, hotMethodCount, dataVaultTokenCount, 1, readFailure);
                    return false;
                }

                string stripped = StripCommentsAndStrings(source);

                for (int tokenIndex = 0; tokenIndex < s_runtimePhaseTokens.Length; tokenIndex++)
                {
                    string token = s_runtimePhaseTokens[tokenIndex];
                    if (stripped.IndexOf(token, StringComparison.Ordinal) >= 0)
                    {
                        result = new ApexVerificationResult1605(files.Length, hotMethodCount, dataVaultTokenCount, 1, file + " contains runtime phase token " + token);
                        return false;
                    }
                }

                for (int tokenIndex = 0; tokenIndex < s_buildSpawnTokens.Length; tokenIndex++)
                {
                    string token = s_buildSpawnTokens[tokenIndex];
                    if (stripped.IndexOf(token, StringComparison.Ordinal) >= 0)
                    {
                        result = new ApexVerificationResult1605(files.Length, hotMethodCount, dataVaultTokenCount, 1, file + " contains build-spawn token " + token);
                        return false;
                    }
                }

                for (int tokenIndex = 0; tokenIndex < s_dataVaultTokens.Length; tokenIndex++)
                {
                    if (stripped.IndexOf(s_dataVaultTokens[tokenIndex], StringComparison.Ordinal) >= 0)
                        dataVaultTokenCount++;
                }

                if (!VerifyHotMethods(file, stripped, ref hotMethodCount, out string hotFailure))
                {
                    result = new ApexVerificationResult1605(files.Length, hotMethodCount, dataVaultTokenCount, 1, hotFailure);
                    return false;
                }

                if (!VerifyDataVaultLocks(file, stripped, out string lockFailure))
                {
                    result = new ApexVerificationResult1605(files.Length, hotMethodCount, dataVaultTokenCount, 1, lockFailure);
                    return false;
                }
            }

            if (!VerifyRequiredMemoryCeilingTokens(files, out string ceilingFailure))
            {
                result = new ApexVerificationResult1605(files.Length, hotMethodCount, dataVaultTokenCount, 1, ceilingFailure);
                return false;
            }

            if (!VerifyRequiredTransactionSafetyTokens(files, out string transactionFailure))
            {
                result = new ApexVerificationResult1605(files.Length, hotMethodCount, dataVaultTokenCount, 1, transactionFailure);
                return false;
            }

            result = new ApexVerificationResult1605(files.Length, hotMethodCount, dataVaultTokenCount, 0, string.Empty);
            return true;
        }

        private static bool TryCollectSourceFiles(string sourceRoot, out string[] files, out string failure)
        {
            files = Array.Empty<string>();
            failure = string.Empty;
            try
            {
                files = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories);
                Array.Sort(files, StringComparer.Ordinal);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                failure = "source file enumeration failed: " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static bool TryReadSourceFile(string file, out string source, out string failure)
        {
            source = string.Empty;
            failure = string.Empty;
            try
            {
                source = File.ReadAllText(file);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                failure = "source file read failed: " + file + " / " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static bool VerifyRequiredMemoryCeilingTokens(string[] files, out string failure)
        {
            failure = string.Empty;
            bool[] tokenFound = new bool[s_requiredMemoryCeilingTokens.Length];

            for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                string file = NormalizePath(files[fileIndex]);
                if (file.EndsWith(VerifierSourceFileName, StringComparison.Ordinal))
                    continue;

                if (!TryReadSourceFile(file, out string source, out failure))
                    return false;

                string stripped = StripCommentsAndStrings(source);
                for (int tokenIndex = 0; tokenIndex < s_requiredMemoryCeilingTokens.Length; tokenIndex++)
                {
                    if (!tokenFound[tokenIndex] &&
                        stripped.IndexOf(s_requiredMemoryCeilingTokens[tokenIndex], StringComparison.Ordinal) >= 0)
                    {
                        tokenFound[tokenIndex] = true;
                    }
                }
            }

            for (int tokenIndex = 0; tokenIndex < tokenFound.Length; tokenIndex++)
            {
                if (!tokenFound[tokenIndex])
                {
                    failure = "missing required memory ceiling token " + s_requiredMemoryCeilingTokens[tokenIndex];
                    return false;
                }
            }

            return true;
        }

        private static bool VerifyRequiredTransactionSafetyTokens(string[] files, out string failure)
        {
            failure = string.Empty;
            bool[] tokenFound = new bool[s_requiredTransactionSafetyTokens.Length];

            for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                string file = NormalizePath(files[fileIndex]);
                if (file.EndsWith(VerifierSourceFileName, StringComparison.Ordinal))
                    continue;

                if (!TryReadSourceFile(file, out string source, out failure))
                    return false;

                string stripped = StripCommentsAndStrings(source);
                for (int tokenIndex = 0; tokenIndex < s_requiredTransactionSafetyTokens.Length; tokenIndex++)
                {
                    if (!tokenFound[tokenIndex] &&
                        stripped.IndexOf(s_requiredTransactionSafetyTokens[tokenIndex], StringComparison.Ordinal) >= 0)
                    {
                        tokenFound[tokenIndex] = true;
                    }
                }
            }

            for (int tokenIndex = 0; tokenIndex < tokenFound.Length; tokenIndex++)
            {
                if (!tokenFound[tokenIndex])
                {
                    failure = "missing required transaction safety token " + s_requiredTransactionSafetyTokens[tokenIndex];
                    return false;
                }
            }

            return true;
        }

        private static bool VerifyHotMethods(string file, string source, ref int hotMethodCount, out string failure)
        {
            failure = string.Empty;
            for (int nameIndex = 0; nameIndex < s_hotMethodNames.Length; nameIndex++)
            {
                string methodName = s_hotMethodNames[nameIndex];
                int searchIndex = 0;
                while (TryFindMethodBody(source, methodName, searchIndex, out int bodyStart, out int bodyEnd, out int nextIndex))
                {
                    hotMethodCount++;
                    string body = source.Substring(bodyStart, bodyEnd - bodyStart);
                    for (int tokenIndex = 0; tokenIndex < s_hotForbiddenTokens.Length; tokenIndex++)
                    {
                        string token = s_hotForbiddenTokens[tokenIndex];
                        if (body.IndexOf(token, StringComparison.Ordinal) >= 0)
                        {
                            failure = file + "::" + methodName + " contains hot lookup token " + token;
                            return false;
                        }
                    }

                    searchIndex = nextIndex;
                }
            }

            return true;
        }

        private static bool VerifyDataVaultLocks(string file, string source, out string failure)
        {
            failure = string.Empty;
            if (!ContainsAny(source, s_dataVaultTokens))
                return true;

            int searchIndex = 0;
            while (TryFindAnyMethodBody(source, searchIndex, out int bodyStart, out int bodyEnd, out int nextIndex))
            {
                string body = source.Substring(bodyStart, bodyEnd - bodyStart);
                int acquireCount = CountAny(body, s_writeLockAcquireTokens);
                if (acquireCount > 1)
                {
                    failure = file + " holds more than one DataVault write lock in one method body";
                    return false;
                }

                if (acquireCount == 1 &&
                    (body.IndexOf("try", StringComparison.Ordinal) < 0 ||
                     body.IndexOf("finally", StringComparison.Ordinal) < 0 ||
                     body.IndexOf("ReleaseWriteLock", StringComparison.Ordinal) < 0))
                {
                    failure = file + " acquires DataVault write lock without strict try/finally release";
                    return false;
                }

                searchIndex = nextIndex;
            }

            return true;
        }

        private static bool TryFindMethodBody(string source, string methodName, int startIndex, out int bodyStart, out int bodyEnd, out int nextIndex)
        {
            bodyStart = 0;
            bodyEnd = 0;
            nextIndex = source.Length;

            int index = startIndex;
            while (index < source.Length)
            {
                index = source.IndexOf(methodName, index, StringComparison.Ordinal);
                if (index < 0)
                    return false;

                int before = index - 1;
                int after = index + methodName.Length;
                if ((before < 0 || !IsIdentifierChar(source[before])) &&
                    (after >= source.Length || !IsIdentifierChar(source[after])) &&
                    TryFindBodyAfterIdentifier(source, after, out bodyStart, out bodyEnd))
                {
                    nextIndex = bodyEnd + 1;
                    return true;
                }

                index = after;
            }

            return false;
        }

        private static bool TryFindAnyMethodBody(string source, int startIndex, out int bodyStart, out int bodyEnd, out int nextIndex)
        {
            bodyStart = 0;
            bodyEnd = 0;
            nextIndex = source.Length;

            for (int i = startIndex; i < source.Length; i++)
            {
                if (!IsIdentifierStart(source[i]))
                    continue;

                int identifierEnd = i + 1;
                while (identifierEnd < source.Length && IsIdentifierChar(source[identifierEnd]))
                    identifierEnd++;

                if (TryFindBodyAfterIdentifier(source, identifierEnd, out bodyStart, out bodyEnd))
                {
                    nextIndex = bodyEnd + 1;
                    return true;
                }

                i = identifierEnd;
            }

            return false;
        }

        private static bool TryFindBodyAfterIdentifier(string source, int index, out int bodyStart, out int bodyEnd)
        {
            bodyStart = 0;
            bodyEnd = 0;

            int cursor = SkipWhitespace(source, index);
            if (cursor >= source.Length || source[cursor] != '(')
                return false;

            int parenEnd = FindMatching(source, cursor, '(', ')');
            if (parenEnd < 0)
                return false;

            cursor = SkipWhitespace(source, parenEnd + 1);
            if (cursor >= source.Length || source[cursor] != '{')
                return false;

            int braceEnd = FindMatching(source, cursor, '{', '}');
            if (braceEnd < 0)
                return false;

            bodyStart = cursor + 1;
            bodyEnd = braceEnd;
            return true;
        }

        private static int FindMatching(string source, int openIndex, char open, char close)
        {
            int depth = 0;
            for (int i = openIndex; i < source.Length; i++)
            {
                char c = source[i];
                if (c == open)
                    depth++;
                else if (c == close)
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static int SkipWhitespace(string source, int index)
        {
            while (index < source.Length && char.IsWhiteSpace(source[index]))
                index++;
            return index;
        }

        private static bool ContainsAny(string source, string[] tokens)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                if (source.IndexOf(tokens[i], StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }

        private static int CountAny(string source, string[] tokens)
        {
            int count = 0;
            for (int i = 0; i < tokens.Length; i++)
            {
                int index = 0;
                while (index < source.Length)
                {
                    index = source.IndexOf(tokens[i], index, StringComparison.Ordinal);
                    if (index < 0)
                        break;
                    count++;
                    index += tokens[i].Length;
                }
            }

            return count;
        }

        private static string StripCommentsAndStrings(string source)
        {
            char[] chars = source.ToCharArray();
            bool lineComment = false;
            bool blockComment = false;
            bool stringLiteral = false;
            bool verbatimString = false;
            bool charLiteral = false;

            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                char next = i + 1 < chars.Length ? chars[i + 1] : '\0';

                if (lineComment)
                {
                    if (c == '\r' || c == '\n')
                        lineComment = false;
                    else
                        chars[i] = ' ';
                    continue;
                }

                if (blockComment)
                {
                    if (c == '*' && next == '/')
                    {
                        chars[i] = ' ';
                        chars[i + 1] = ' ';
                        i++;
                        blockComment = false;
                    }
                    else if (c != '\r' && c != '\n')
                    {
                        chars[i] = ' ';
                    }
                    continue;
                }

                if (stringLiteral)
                {
                    if (verbatimString && c == '"' && next == '"')
                    {
                        chars[i] = ' ';
                        chars[i + 1] = ' ';
                        i++;
                        continue;
                    }

                    bool close = c == '"' && (verbatimString || !IsEscaped(source, i));
                    if (c != '\r' && c != '\n')
                        chars[i] = ' ';
                    if (close)
                    {
                        stringLiteral = false;
                        verbatimString = false;
                    }
                    continue;
                }

                if (charLiteral)
                {
                    bool close = c == '\'' && !IsEscaped(source, i);
                    if (c != '\r' && c != '\n')
                        chars[i] = ' ';
                    if (close)
                        charLiteral = false;
                    continue;
                }

                if (c == '/' && next == '/')
                {
                    chars[i] = ' ';
                    chars[i + 1] = ' ';
                    i++;
                    lineComment = true;
                    continue;
                }

                if (c == '/' && next == '*')
                {
                    chars[i] = ' ';
                    chars[i + 1] = ' ';
                    i++;
                    blockComment = true;
                    continue;
                }

                if (c == '@' && next == '"')
                {
                    chars[i] = ' ';
                    chars[i + 1] = ' ';
                    i++;
                    stringLiteral = true;
                    verbatimString = true;
                    continue;
                }

                if (c == '"')
                {
                    chars[i] = ' ';
                    stringLiteral = true;
                    verbatimString = false;
                    continue;
                }

                if (c == '\'')
                {
                    chars[i] = ' ';
                    charLiteral = true;
                }
            }

            return new string(chars);
        }

        private static bool IsEscaped(string source, int index)
        {
            int slashCount = 0;
            for (int i = index - 1; i >= 0 && source[i] == '\\'; i--)
                slashCount++;
            return (slashCount & 1) == 1;
        }

        private static bool IsIdentifierStart(char c)
        {
            return c == '_' || char.IsLetter(c);
        }

        private static bool IsIdentifierChar(char c)
        {
            return c == '_' || char.IsLetterOrDigit(c);
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}
