namespace Hecton8.Tests.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;
    using NUnit.Framework;

    public sealed class ApexIntegratorVerification1619EditTests
    {
        private static readonly string[] HotDependencyTokens =
        {
            "GlobalRegistry.",
            "GlobalRegistry.Get<",
            "GlobalRegistry.Get(",
            "GlobalRegistry.Dispatcher",
            "GetComponent<",
            "GetComponents<",
            "GetComponentInChildren<",
            "GetComponentInParent<",
            "TryGetComponent<",
            "FindObjectOfType",
            "FindObjectsOfType",
            "GameObject.Find",
            "Camera.main"
        };

        private static readonly string[] SimulationPresentationTokens =
        {
            "Shader.SetGlobal",
            "Graphics.Draw",
            "Graphics.Render",
            ".material",
            ".materials",
            ".text =",
            "SetText(",
            "SetCharArray(",
            "SetParticles(",
            "GetParticles(",
            ".localRotation",
            ".color =",
            ".intensity =",
            ".range =",
            ".spotAngle ="
        };

        private static readonly string[] RuntimeBuildLauncherTokens =
        {
            "ProcessStartInfo",
            "Process.Start(",
            "BuildPipeline.BuildPlayer"
        };

        private static readonly Regex MethodRegex = new Regex(
            "(?:(?:public|private|protected|internal|static|sealed|override|virtual|unsafe|partial|readonly|async|new)\\s+)+[\\w<>\\[\\],\\.]+\\s+(?:(?:\\w+\\.)?(?<name>[A-Za-z_][A-Za-z0-9_]*))\\s*\\([^;{}]*\\)\\s*(?:where\\s+[^{}]+)?\\{|[\\w<>\\[\\],\\.]+\\s+(?<explicitName>[A-Za-z_][A-Za-z0-9_]*\\.[A-Za-z_][A-Za-z0-9_]*)\\s*\\([^;{}]*\\)\\s*\\{",
            RegexOptions.Compiled);

        [Test]
        public void HotPathDependenciesAreColdCached()
        {
            foreach (string file in EnumerateRuntimeSourceFiles())
            {
                string source = StripCommentsAndStrings(File.ReadAllText(file));
                List<SourceMethod> methods = ExtractMethods(source);
                for (int i = 0; i < methods.Count; i++)
                {
                    SourceMethod method = methods[i];
                    if (!IsHotPathMethod(method.Name))
                        continue;

                    AssertNoToken(file, method, method.Body, HotDependencyTokens);
                    AssertDirectLocalHelpersClean(file, method, methods, HotDependencyTokens);
                }
            }
        }

        [Test]
        public void PresentationWritesStayOutOfSimulationPhases()
        {
            foreach (string file in EnumerateRuntimeSourceFiles())
            {
                string source = StripCommentsAndStrings(File.ReadAllText(file));
                List<SourceMethod> methods = ExtractMethods(source);
                for (int i = 0; i < methods.Count; i++)
                {
                    SourceMethod method = methods[i];
                    if (!IsSimulationPhaseMethod(method.Name))
                        continue;

                    AssertNoToken(file, method, method.Body, SimulationPresentationTokens);
                    AssertDirectLocalHelpersClean(file, method, methods, SimulationPresentationTokens);
                }
            }
        }

        [Test]
        public void DataVaultWriteLocksAreSingleScopeTryFinally()
        {
            foreach (string file in EnumerateRuntimeSourceFiles())
            {
                string source = StripCommentsAndStrings(File.ReadAllText(file));
                List<SourceMethod> methods = ExtractMethods(source);
                for (int i = 0; i < methods.Count; i++)
                {
                    SourceMethod method = methods[i];
                    if (!ContainsDataVaultWriteLockAcquire(method.Body))
                        continue;

                    Assert.That(HasNestedDataVaultWriteLockAcquire(method.Body), Is.False, file + ":" + method.Line + " " + method.Name + " can acquire a second DataVault write lock before releasing the first.");
                    if (ContainsDataVaultWriteLockRelease(method.Body))
                    {
                        Assert.That(method.Body, Does.Contain("finally"), file + ":" + method.Line + " " + method.Name + " releases a DataVault write lock without strict finally release.");
                    }
                }
            }
        }

        [Test]
        public void RuntimeCodeDoesNotLaunchCompilationOrBuildProcesses()
        {
            foreach (string file in EnumerateRuntimeSourceFiles())
            {
                string source = StripCommentsAndStrings(File.ReadAllText(file));
                AssertNoToken(file, default, source, RuntimeBuildLauncherTokens);
            }
        }

        private static IEnumerable<string> EnumerateRuntimeSourceFiles()
        {
            string root = Directory.GetCurrentDirectory();
            string scriptsRoot = Path.Combine(root, "Assets", "_Project", "Scripts");
            foreach (string file in Directory.EnumerateFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string normalized = file.Replace('\\', '/');
                if (normalized.Contains("/Editor/", StringComparison.Ordinal) ||
                    normalized.Contains("/Tests/", StringComparison.Ordinal) ||
                    normalized.EndsWith(".Editor.cs", StringComparison.Ordinal))
                {
                    continue;
                }

                yield return file;
            }
        }

        private static List<SourceMethod> ExtractMethods(string source)
        {
            List<SourceMethod> methods = new List<SourceMethod>(128);
            MatchCollection matches = MethodRegex.Matches(source);
            for (int i = 0; i < matches.Count; i++)
            {
                Match match = matches[i];
                int bodyStart = match.Value.LastIndexOf('{');
                if (bodyStart < 0)
                    continue;

                int braceIndex = match.Index + bodyStart;
                int endIndex = FindMatchingBrace(source, braceIndex);
                if (endIndex <= braceIndex)
                    continue;

                string rawName = match.Groups["name"].Success ? match.Groups["name"].Value : match.Groups["explicitName"].Value;
                int dotIndex = rawName.LastIndexOf('.');
                string methodName = dotIndex >= 0 ? rawName.Substring(dotIndex + 1) : rawName;
                methods.Add(new SourceMethod(methodName, source.Substring(braceIndex, endIndex - braceIndex + 1), CountLines(source, match.Index) + 1));
            }

            return methods;
        }

        private static int FindMatchingBrace(string source, int braceIndex)
        {
            int depth = 0;
            for (int i = braceIndex; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static bool IsHotPathMethod(string methodName)
        {
            return methodName == "Tick" ||
                   methodName == "FixedTick" ||
                   methodName == "LateFrameTick" ||
                   methodName == "Update" ||
                   methodName == "FixedUpdate" ||
                   methodName == "LateUpdate" ||
                   methodName == "Execute" ||
                   methodName == "OnUpdate";
        }

        private static bool IsSimulationPhaseMethod(string methodName)
        {
            return methodName == "Tick" ||
                   methodName == "FixedTick" ||
                   methodName == "Update" ||
                   methodName == "FixedUpdate" ||
                   methodName == "OnUpdate";
        }

        private static void AssertDirectLocalHelpersClean(
            string file,
            SourceMethod hotMethod,
            List<SourceMethod> methods,
            string[] forbiddenTokens)
        {
            for (int i = 0; i < methods.Count; i++)
            {
                SourceMethod helper = methods[i];
                if (helper.Name == hotMethod.Name)
                    continue;

                if (!ContainsCallTo(hotMethod.Body, helper.Name))
                    continue;

                AssertNoToken(file, helper, helper.Body, forbiddenTokens);
            }
        }

        private static bool ContainsCallTo(string body, string methodName)
        {
            int index = body.IndexOf(methodName + "(", StringComparison.Ordinal);
            if (index < 0)
                return false;

            if (index > 0)
            {
                char previous = body[index - 1];
                if (char.IsLetterOrDigit(previous) || previous == '_' || previous == '.')
                    return false;
            }

            return true;
        }

        private static void AssertNoToken(string file, SourceMethod method, string source, string[] forbiddenTokens)
        {
            for (int i = 0; i < forbiddenTokens.Length; i++)
            {
                string token = forbiddenTokens[i];
                Assert.That(
                    source.Contains(token, StringComparison.Ordinal),
                    Is.False,
                    file + ":" + method.Line + " " + method.Name + " contains forbidden token " + token);
            }
        }

        private static bool ContainsDataVaultWriteLockAcquire(string source)
        {
            return source.Contains("TryAcquireWriteLock(", StringComparison.Ordinal) ||
                   source.Contains("TryAcquireMutationGuard(", StringComparison.Ordinal) ||
                   source.Contains("TryLockBuffer(", StringComparison.Ordinal);
        }

        private static bool ContainsDataVaultWriteLockRelease(string source)
        {
            return source.Contains("ReleaseWriteLock(", StringComparison.Ordinal) ||
                   source.Contains("ReleaseMutationGuard(", StringComparison.Ordinal) ||
                   source.Contains("TryUnlockBuffer(", StringComparison.Ordinal);
        }

        private static bool HasNestedDataVaultWriteLockAcquire(string source)
        {
            string[] acquireTokens =
            {
                "TryAcquireWriteLock(",
                "TryAcquireMutationGuard(",
                "TryLockBuffer("
            };
            string[] releaseTokens =
            {
                "ReleaseWriteLock(",
                "ReleaseMutationGuard(",
                "TryUnlockBuffer("
            };
            bool held = false;
            int index = 0;
            while (index < source.Length)
            {
                int acquireIndex = FindNextToken(source, acquireTokens, index, out int acquireLength);
                int releaseIndex = FindNextToken(source, releaseTokens, index, out int releaseLength);
                if (acquireIndex < 0 && releaseIndex < 0)
                    return false;

                if (acquireIndex >= 0 && (releaseIndex < 0 || acquireIndex < releaseIndex))
                {
                    if (held)
                        return true;
                    held = true;
                    index = acquireIndex + acquireLength;
                    continue;
                }

                held = false;
                index = releaseIndex + releaseLength;
            }

            return false;
        }

        private static int FindNextToken(string source, string[] tokens, int startIndex, out int tokenLength)
        {
            int bestIndex = -1;
            tokenLength = 0;
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                int index = source.IndexOf(token, startIndex, StringComparison.Ordinal);
                if (index < 0 || (bestIndex >= 0 && index >= bestIndex))
                    continue;

                bestIndex = index;
                tokenLength = token.Length;
            }

            return bestIndex;
        }

        private static int CountLines(string source, int endExclusive)
        {
            int lines = 0;
            for (int i = 0; i < endExclusive && i < source.Length; i++)
                if (source[i] == '\n')
                    lines++;

            return lines;
        }

        private static string StripCommentsAndStrings(string source)
        {
            char[] buffer = source.ToCharArray();
            bool inString = false;
            bool inVerbatimString = false;
            bool inChar = false;
            bool inLineComment = false;
            bool inBlockComment = false;

            for (int i = 0; i < buffer.Length; i++)
            {
                char c = buffer[i];
                char next = i + 1 < buffer.Length ? buffer[i + 1] : '\0';

                if (inLineComment)
                {
                    if (c == '\n')
                        inLineComment = false;
                    else
                        buffer[i] = ' ';
                    continue;
                }

                if (inBlockComment)
                {
                    if (c == '*' && next == '/')
                    {
                        buffer[i] = ' ';
                        buffer[i + 1] = ' ';
                        i++;
                        inBlockComment = false;
                    }
                    else if (c != '\n')
                    {
                        buffer[i] = ' ';
                    }

                    continue;
                }

                if (inString)
                {
                    if (inVerbatimString && c == '"' && next == '"')
                    {
                        buffer[i] = ' ';
                        buffer[i + 1] = ' ';
                        i++;
                        continue;
                    }

                    if (!inVerbatimString && c == '\\' && i + 1 < buffer.Length)
                    {
                        buffer[i] = ' ';
                        buffer[i + 1] = ' ';
                        i++;
                        continue;
                    }

                    if (c == '"')
                    {
                        inString = false;
                        inVerbatimString = false;
                    }

                    if (c != '\n')
                        buffer[i] = ' ';
                    continue;
                }

                if (inChar)
                {
                    if (c == '\\' && i + 1 < buffer.Length)
                    {
                        buffer[i] = ' ';
                        buffer[i + 1] = ' ';
                        i++;
                        continue;
                    }

                    if (c == '\'')
                        inChar = false;
                    if (c != '\n')
                        buffer[i] = ' ';
                    continue;
                }

                if (c == '/' && next == '/')
                {
                    buffer[i] = ' ';
                    buffer[i + 1] = ' ';
                    i++;
                    inLineComment = true;
                    continue;
                }

                if (c == '/' && next == '*')
                {
                    buffer[i] = ' ';
                    buffer[i + 1] = ' ';
                    i++;
                    inBlockComment = true;
                    continue;
                }

                if (c == '@' && next == '"')
                {
                    buffer[i] = ' ';
                    buffer[i + 1] = ' ';
                    i++;
                    inString = true;
                    inVerbatimString = true;
                    continue;
                }

                if (c == '"')
                {
                    buffer[i] = ' ';
                    inString = true;
                    continue;
                }

                if (c == '\'')
                {
                    buffer[i] = ' ';
                    inChar = true;
                }
            }

            return new string(buffer);
        }

        private readonly struct SourceMethod
        {
            public SourceMethod(string name, string body, int line)
            {
                Name = name;
                Body = body;
                Line = line;
            }

            public string Name { get; }
            public string Body { get; }
            public int Line { get; }
        }
    }
}
