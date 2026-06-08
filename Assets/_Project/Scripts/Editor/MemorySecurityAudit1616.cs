#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hecton8.Core;

namespace Hecton8.EditorTools
{
    internal static unsafe class MemorySecurityAudit1616
    {
        private const string MenuPath = "Hecton/Validation/Core/Run Memory Security Audit 1616";
        private const string StressPath = "Hecton/Validation/Core/Run Memory Sentinel Stress 1616";
        private const string RuntimeRoot = "Assets/_Project/Scripts";
        private const int RecorderCapacity = 8;
        private const int RegistrationStressIterations = 10000;
        private const int HotSwapStressIterations = 1000;
        private const int MaxAuditViolationCount = 128;
        private const char OpenBrace = (char)123;
        private const char CloseBrace = (char)125;
        private const string ComponentToken = "Component";
        private const string ComponentsToken = "Components";
        private static readonly string[] GcAllocCounters = { "GC Allocated In Frame" };

        private static readonly string[] NativeAllocationTokens =
        {
            "UnsafeUtility.Malloc",
            "new NativeArray<",
            "new NativeList<",
            "new NativeQueue<",
            "new NativeHashMap<",
            "new NativeParallelHashMap<",
            "new NativeParallelHashSet<",
            "new NativeParallelMultiHashMap<"
        };

        private static readonly string[] NativeReleaseTokens =
        {
            ".Dispose(",
            "UnsafeUtility.Free",
            "H8Memory.Release",
            "NativeMemorySentinel.Unregister",
            "UnregisterNative",
            "UnregisterPointer"
        };

        private static readonly string[] NativeTrackingTokens =
        {
            "NativeMemorySentinel.Register",
            "NativeMemoryTrackingBridge.Register",
            "H8Memory.Allocate",
            "H8Memory.AllocateRaw",
            "GlobalDataVault.CreateBuffer",
            "TryCreateBuffer",
            "CreateOrResizeBuffer",
            "VaultGenerationHandle<"
        };

        private static readonly string[] LockTokens =
        {
            "TryAcquireWriteLock",
            "TryLockBuffer"
        };

        private static readonly string[] WriteReleaseLockTokens =
        {
            "ReleaseWriteLock"
        };

        private static readonly string[] PinReleaseLockTokens =
        {
            "TryUnlockBuffer"
        };

        private static readonly string[] WriteLockTokens =
        {
            "TryAcquireWriteLock"
        };

        private static readonly string[] HotMethodNames =
        {
            "Tick",
            "FixedTick",
            "LateFrameTick",
            "Update",
            "FixedUpdate",
            "LateUpdate",
            "Execute",
            "VisualSyncTick"
        };

        private static readonly string[] HotMethodNeedles =
        {
            "Tick(",
            "FixedTick(",
            "LateFrameTick(",
            "Update(",
            "FixedUpdate(",
            "LateUpdate(",
            "Execute(",
            "VisualSyncTick("
        };

        private static readonly string[] SimulationMethodNames =
        {
            "Tick",
            "FixedTick",
            "Update",
            "FixedUpdate",
            "Execute"
        };

        private static readonly string[] SimulationMethodNeedles =
        {
            "Tick(",
            "FixedTick(",
            "Update(",
            "FixedUpdate(",
            "Execute("
        };

        private static readonly string[] ForbiddenHotLookupTokens =
        {
            "GlobalRegistry.Get",
            "GlobalRegistry.Resolve",
            "GlobalRegistry.",
            "Get" + ComponentToken + "(",
            "Get" + ComponentToken + "<",
            "TryGet" + ComponentToken + "(",
            "TryGet" + ComponentToken + "<",
            "Get" + ComponentsToken + "(",
            "Get" + ComponentsToken + "<",
            "GameObject.Find",
            "FindObjectOfType",
            "Camera.main"
        };

        private static readonly string[] PresentationWriteTokens =
        {
            "SetGlobalFloat",
            "SetGlobalInt",
            "SetGlobalVector",
            "SetGlobalColor",
            "SetGlobalTexture",
            "SetGlobalBuffer",
            "SetPropertyBlock",
            ".material",
            ".materials",
            "Graphics.Draw",
            "CommandBuffer",
            "AudioSource.Play",
            "PlayOneShot",
            "ParticleSystem.Emit",
            "SetVertices",
            "SetIndices",
            "SetColors",
            "SetMesh"
        };

        private static readonly string[] CriticalStaticResetFiles =
        {
            "Assets/_Project/Scripts/Core/GlobalRegistry.cs",
            "Assets/_Project/Scripts/Core/SystemDispatcher.cs",
            "Assets/_Project/Scripts/Core/NativeMemorySentinel.cs",
            "Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs"
        };

        private struct AuditStats
        {
            public int ScriptCount;
            public int NativeAllocationFiles;
            public int NativeTrackedFiles;
            public int HotSwapCacheSites;
            public int ListenerSites;
            public int LockSites;
            public int NestedLockViolations;
            public int HotMethodBodies;
            public int SimulationMethodBodies;
            public int PresentationPhaseViolations;
            public int StaticResetFiles;
            public int Violations;
            public long ElapsedMicroseconds;
            public string SourceHash;
        }

        private struct FileScanPlan
        {
            public bool NeedsScan;
            public bool NativeLifecycle;
            public bool HotSwapContracts;
            public bool ListenerUnsubscription;
            public bool LockReleaseDiscipline;
            public bool HotPathRegistryPolling;
            public bool SimulationPhasePresentationWrites;
        }

        [MenuItem(MenuPath, priority = 1616)]
        private static void RunAuditMenu()
        {
            if (!CanRunHeavyAuditNow("audit"))
                return;

            try
            {
                AuditStats stats = RunAudit();
                UnityEngine.Debug.Log(
                    "[MemorySecurityAudit1616] PASS files=" + stats.ScriptCount +
                    " nativeFiles=" + stats.NativeAllocationFiles +
                    " nativeTrackedFiles=" + stats.NativeTrackedFiles +
                    " hotSwapCacheSites=" + stats.HotSwapCacheSites +
                    " listenerSites=" + stats.ListenerSites +
                    " lockSites=" + stats.LockSites +
                    " nestedLockViolations=" + stats.NestedLockViolations +
                    " hotMethods=" + stats.HotMethodBodies +
                    " simulationMethods=" + stats.SimulationMethodBodies +
                    " presentationPhaseViolations=" + stats.PresentationPhaseViolations +
                    " staticResets=" + stats.StaticResetFiles +
                    " elapsedUs=" + stats.ElapsedMicroseconds +
                    " hash=" + stats.SourceHash);
            }
            catch (InvalidOperationException exception)
            {
                UnityEngine.Debug.LogError("[MemorySecurityAudit1616] FAIL " + exception.Message);
            }
        }

        [MenuItem(StressPath, priority = 1617)]
        private static void RunStressMenu()
        {
            if (!CanRunHeavyAuditNow("stress"))
                return;

            RunMockLeakDetectionProbe();
            RunExplicitSceneUnregisterProbe();
            RunHotSwapRebindProbe();
            RunZeroGcRegistrationProbe();
            UnityEngine.Debug.Log(
                "[MemorySecurityAudit1616] STRESS PASS mockLeak=pass explicitSceneUnregister=pass hotSwapIterations=" + HotSwapStressIterations +
                " zeroGcRegistrationIterations=" + RegistrationStressIterations);
        }

        private static bool CanRunHeavyAuditNow(string auditName)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                UnityEngine.Debug.Log(
                    "[MemorySecurityAudit1616] SKIP " +
                    auditName +
                    " while editor is playing, compiling, or importing. Run validation from idle EditMode.");
                return false;
            }

            return true;
        }

        private static AuditStats RunAudit()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            AuditStats stats = default;
            List<string> violations = new List<string>(256);

            string[] files = Directory.GetFiles(RuntimeRoot, "*.cs", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            using (SHA256 sha = SHA256.Create())
            {
                for (int i = 0; i < files.Length && violations.Count < MaxAuditViolationCount; i++)
                {
                    string path = NormalizePath(files[i]);
                    string text = File.ReadAllText(path);
                    stats.ScriptCount++;
                    FeedHash(sha, path);
                    FileScanPlan plan = CreateFileScanPlan(path, text);
                    if (!plan.NeedsScan)
                        continue;

                    FeedHash(sha, text);
                    string code = StripCommentsAndStrings(text);
                    if (plan.NativeLifecycle && violations.Count < MaxAuditViolationCount)
                        AuditNativeLifecycle(path, code, violations, ref stats);
                    ClampViolationList(violations);
                    if (plan.HotSwapContracts && violations.Count < MaxAuditViolationCount)
                        AuditHotSwapContracts(path, code, violations, ref stats);
                    ClampViolationList(violations);
                    if (plan.ListenerUnsubscription && violations.Count < MaxAuditViolationCount)
                        AuditListenerUnsubscription(path, code, violations, ref stats);
                    ClampViolationList(violations);
                    if (plan.LockReleaseDiscipline && violations.Count < MaxAuditViolationCount)
                        AuditLockReleaseDiscipline(path, code, violations, ref stats);
                    ClampViolationList(violations);
                    if (plan.HotPathRegistryPolling && violations.Count < MaxAuditViolationCount)
                        AuditHotPathRegistryPolling(path, code, violations, ref stats);
                    ClampViolationList(violations);
                    if (plan.SimulationPhasePresentationWrites && violations.Count < MaxAuditViolationCount)
                        AuditSimulationPhasePresentationWrites(path, code, violations, ref stats);
                    ClampViolationList(violations);
                }

                stats.SourceHash = FinishHash(sha);
            }

            if (violations.Count < MaxAuditViolationCount)
                AuditStaticResetContracts(violations, ref stats);
            stopwatch.Stop();
            stats.ElapsedMicroseconds = stopwatch.ElapsedTicks * 1000000L / Stopwatch.Frequency;
            stats.Violations = violations.Count;

            if (violations.Count > 0)
                throw new InvalidOperationException(BuildViolationMessage(violations, stats));

            return stats;
        }

        private static void ClampViolationList(List<string> violations)
        {
            if (violations.Count > MaxAuditViolationCount)
                violations.RemoveRange(MaxAuditViolationCount, violations.Count - MaxAuditViolationCount);
        }

        private static FileScanPlan CreateFileScanPlan(string path, string text)
        {
            FileScanPlan plan = default;
            if (path.EndsWith("/GlobalRegistry.cs", StringComparison.Ordinal) ||
                path.EndsWith("/SystemDispatcher.cs", StringComparison.Ordinal) ||
                path.EndsWith("/NativeMemorySentinel.cs", StringComparison.Ordinal) ||
                path.EndsWith("/GameBootstrapper.cs", StringComparison.Ordinal))
            {
                plan.NeedsScan = true;
            }

            plan.NativeLifecycle = ContainsAny(text, NativeAllocationTokens);
            plan.LockReleaseDiscipline = ContainsAny(text, LockTokens);
            plan.HotSwapContracts = MightCacheRegistryDependency(text) ||
                                    text.IndexOf("RegisterHotSwapListener(", StringComparison.Ordinal) >= 0;
            plan.ListenerUnsubscription = text.IndexOf("RegisterListener(", StringComparison.Ordinal) >= 0;
            plan.HotPathRegistryPolling = ContainsAny(text, HotMethodNeedles) && ContainsAny(text, ForbiddenHotLookupTokens);
            plan.SimulationPhasePresentationWrites = ContainsAny(text, SimulationMethodNeedles) && ContainsAny(text, PresentationWriteTokens);
            plan.NeedsScan = plan.NeedsScan ||
                             plan.NativeLifecycle ||
                             plan.LockReleaseDiscipline ||
                             plan.HotSwapContracts ||
                             plan.ListenerUnsubscription ||
                             plan.HotPathRegistryPolling ||
                             plan.SimulationPhasePresentationWrites;

            return plan;
        }

        private static bool MightCacheRegistryDependency(string text)
        {
            int position = 0;
            while ((position = text.IndexOf("GlobalRegistry.", position, StringComparison.Ordinal)) >= 0)
            {
                int lineStart = text.LastIndexOf('\n', position);
                int lineEnd = text.IndexOf('\n', position);
                lineStart = lineStart < 0 ? 0 : lineStart + 1;
                lineEnd = lineEnd < 0 ? text.Length : lineEnd;
                int length = lineEnd - lineStart;
                if (length > 0)
                {
                    string line = text.Substring(lineStart, length);
                    if (line.IndexOf('=') >= 0 &&
                        line.IndexOf("==", StringComparison.Ordinal) < 0 &&
                        line.IndexOf("!=", StringComparison.Ordinal) < 0 &&
                        (line.IndexOf('_') >= 0 || line.IndexOf("this.", StringComparison.Ordinal) >= 0))
                    {
                        return true;
                    }
                }

                position += "GlobalRegistry.".Length;
            }

            return false;
        }

        private static void AuditNativeLifecycle(string path, string code, List<string> violations, ref AuditStats stats)
        {
            if (!ContainsAny(code, NativeAllocationTokens))
                return;

            stats.NativeAllocationFiles++;
            bool isEditorOnly = IsEditorPath(path);
            bool hasRelease = ContainsAny(code, NativeReleaseTokens);
            bool trackedByNativeOwner = ContainsAny(code, NativeTrackingTokens) ||
                                        path.EndsWith("/NativeMemorySentinel.cs", StringComparison.Ordinal) ||
                                        path.EndsWith("/H8Memory.cs", StringComparison.Ordinal) ||
                                        path.EndsWith("/GlobalDataVault.cs", StringComparison.Ordinal);
            if (trackedByNativeOwner)
                stats.NativeTrackedFiles++;

            if (!hasRelease)
                violations.Add(path + ": native allocation token exists without a local Dispose/Free/release token.");

            if (!isEditorOnly && !trackedByNativeOwner)
                violations.Add(path + ": runtime native allocation token exists without Sentinel, H8Memory, or DataVault ownership token.");
        }

        private static void AuditHotSwapContracts(string path, string code, List<string> violations, ref AuditStats stats)
        {
            int position = 0;
            while (TryFindNextClass(code, ref position, out int headerStart, out int bodyStart, out int bodyEnd))
            {
                string header = code.Substring(headerStart, bodyStart - headerStart);
                string body = code.Substring(bodyStart, bodyEnd - bodyStart);
                bool cachesRegistryDependency = ContainsCachedRegistryAssignment(body);
                bool registersHotSwap = body.IndexOf("RegisterHotSwapListener(this", StringComparison.Ordinal) >= 0 ||
                                        body.IndexOf("RegisterHotSwapListener(s_", StringComparison.Ordinal) >= 0;
                if (cachesRegistryDependency)
                {
                    stats.HotSwapCacheSites++;
                    if (header.IndexOf("IGlobalRegistryHotSwapListener", StringComparison.Ordinal) < 0 &&
                        body.IndexOf("IGlobalRegistryHotSwapListener", StringComparison.Ordinal) < 0)
                    {
                        violations.Add(path + ": cached GlobalRegistry dependency without IGlobalRegistryHotSwapListener.");
                    }
                }

                if (registersHotSwap &&
                    body.IndexOf("UnregisterHotSwapListener(this", StringComparison.Ordinal) < 0 &&
                    body.IndexOf("TryUnregisterHotSwapListener(this", StringComparison.Ordinal) < 0 &&
                    body.IndexOf("UnregisterHotSwapListener(s_", StringComparison.Ordinal) < 0 &&
                    body.IndexOf("TryUnregisterHotSwapListener(s_", StringComparison.Ordinal) < 0)
                {
                    violations.Add(path + ": hot-swap listener registration lacks matching unregister call.");
                }
            }
        }

        private static void AuditListenerUnsubscription(string path, string code, List<string> violations, ref AuditStats stats)
        {
            int position = 0;
            while (TryFindNextClass(code, ref position, out int headerStart, out int bodyStart, out int bodyEnd))
            {
                string body = code.Substring(bodyStart, bodyEnd - bodyStart);
                bool registersSelfListener = body.IndexOf("RegisterListener(this", StringComparison.Ordinal) >= 0;
                bool registersStaticListener = body.IndexOf("RegisterListener(s_", StringComparison.Ordinal) >= 0;
                if (!registersSelfListener && !registersStaticListener)
                    continue;

                stats.ListenerSites++;
                if (registersSelfListener && body.IndexOf("UnregisterListener(this", StringComparison.Ordinal) < 0)
                    violations.Add(path + ": listener registration on this lacks matching unregister.");

                if (registersStaticListener && body.IndexOf("UnregisterListener(s_", StringComparison.Ordinal) < 0)
                    violations.Add(path + ": static listener registration lacks matching unregister.");
            }
        }

        private static void AuditLockReleaseDiscipline(string path, string code, List<string> violations, ref AuditStats stats)
        {
            for (int tokenIndex = 0; tokenIndex < LockTokens.Length; tokenIndex++)
            {
                string token = LockTokens[tokenIndex];
                int position = 0;
                while ((position = code.IndexOf(token, position, StringComparison.Ordinal)) >= 0)
                {
                    if (!IsMemberInvocation(code, position, token))
                    {
                        position += token.Length;
                        continue;
                    }

                    stats.LockSites++;
                    string[] releaseTokens = ResolveReleaseTokens(token);
                    if (!HasFinallyReleaseNearLock(code, position, releaseTokens))
                        violations.Add(path + ":" + CountLineNumber(code, position) + " " + token + " lacks nearby finally release.");
                    if (token == "TryAcquireWriteLock" &&
                        HasNestedLockBeforeRelease(code, position, WriteLockTokens, releaseTokens))
                    {
                        stats.NestedLockViolations++;
                        violations.Add(path + ":" + CountLineNumber(code, position) + " " + token + " can acquire a second write lock before release.");
                    }
                    position += token.Length;
                }
            }
        }

        private static void AuditHotPathRegistryPolling(string path, string code, List<string> violations, ref AuditStats stats)
        {
            if (IsEditorPath(path) || path.EndsWith("/GlobalRegistry.cs", StringComparison.Ordinal))
                return;

            for (int i = 0; i < HotMethodNames.Length; i++)
            {
                int search = 0;
                while (TryFindMethodBody(code, HotMethodNames[i], ref search, out int bodyStart, out int bodyEnd))
                {
                    stats.HotMethodBodies++;
                    string body = code.Substring(bodyStart, bodyEnd - bodyStart);
                    string forbiddenToken = FindFirstToken(body, ForbiddenHotLookupTokens);
                    if (!string.IsNullOrEmpty(forbiddenToken))
                    {
                        violations.Add(path + ":" + CountLineNumber(code, bodyStart) + " hot method " + HotMethodNames[i] + " contains forbidden lookup " + forbiddenToken + ".");
                    }
                }
            }
        }

        private static void AuditSimulationPhasePresentationWrites(string path, string code, List<string> violations, ref AuditStats stats)
        {
            if (IsEditorPath(path))
                return;

            for (int i = 0; i < SimulationMethodNames.Length; i++)
            {
                int search = 0;
                while (TryFindMethodBody(code, SimulationMethodNames[i], ref search, out int bodyStart, out int bodyEnd))
                {
                    stats.SimulationMethodBodies++;
                    string body = code.Substring(bodyStart, bodyEnd - bodyStart);
                    string token = FindFirstToken(body, PresentationWriteTokens);
                    if (string.IsNullOrEmpty(token))
                        continue;

                    stats.PresentationPhaseViolations++;
                    violations.Add(path + ":" + CountLineNumber(code, bodyStart) + " simulation method " + SimulationMethodNames[i] + " contains presentation write " + token + "; move to LateFrameTick or VisualSync.");
                }
            }
        }

        private static void AuditStaticResetContracts(List<string> violations, ref AuditStats stats)
        {
            for (int i = 0; i < CriticalStaticResetFiles.Length; i++)
            {
                string path = CriticalStaticResetFiles[i];
                if (!File.Exists(path))
                {
                    violations.Add(path + ": critical static reset file missing.");
                    continue;
                }

                string code = StripCommentsAndStrings(File.ReadAllText(path));
                if (code.IndexOf("RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)", StringComparison.Ordinal) < 0)
                {
                    violations.Add(path + ": missing SubsystemRegistration reset hook.");
                    continue;
                }

                stats.StaticResetFiles++;
            }
        }

        private static void RunMockLeakDetectionProbe()
        {
            void* pointer = UnsafeUtility.Malloc(64, 16, Allocator.Persistent);
            if (pointer == null)
                throw new InvalidOperationException("Mock scene leak probe could not allocate native memory.");

            int id = 0;
            FixedString128Bytes owner = default;
            FixedString128Bytes label = default;
            owner.CopyFromTruncated("MemorySecurityAudit1616");
            label.CopyFromTruncated("MockSceneLeakProbe");
            try
            {
                id = NativeMemorySentinel.RegisterPointer(pointer, 64, in owner, in label, NativeAllocationLifetime.Scene);
                if (id <= 0)
                    throw new InvalidOperationException("Mock scene leak probe registration failed.");

                NativeMemorySentinel.BeginDiagnosticSceneLeakLogSuppression();
                try
                {
                    NativeMemorySentinel.AssertNoSceneLifetimeAllocationsForDiagnostics("MemorySecurityAudit1616.MockScene");
                    throw new InvalidOperationException("Mock scene leak probe did not throw.");
                }
                catch (FatalMemoryLeakException exception)
                {
                    if (exception.Message.IndexOf("MockSceneLeakProbe", StringComparison.Ordinal) < 0)
                        throw new InvalidOperationException("Mock scene leak payload omitted buffer label.");
                }
                finally
                {
                    NativeMemorySentinel.EndDiagnosticSceneLeakLogSuppression();
                }
            }
            finally
            {
                if (pointer != null)
                {
                    UnsafeUtility.Free(pointer, Allocator.Persistent);
                    pointer = null;
                }

                NativeMemorySentinel.Unregister(id);
            }
        }

        private static void RunHotSwapRebindProbe()
        {
            HotSwapProbe probe = new HotSwapProbe();
            object previous = null;
            object[] services = new object[HotSwapStressIterations];
            for (int i = 0; i < services.Length; i++)
                services[i] = new object();

            for (int i = 0; i < services.Length; i++)
            {
                object current = services[i];
                probe.OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot.Audio, previous, current);
                if (!ReferenceEquals(probe.CurrentService, current))
                    throw new InvalidOperationException("Hot-swap probe retained a stale service reference at iteration " + i);

                previous = current;
            }

            if (probe.RebindCount != services.Length)
                throw new InvalidOperationException("Hot-swap probe missed callbacks: " + probe.RebindCount);
        }

        private static void RunExplicitSceneUnregisterProbe()
        {
            Scene scene = SceneManager.GetActiveScene();
            void* pointer = UnsafeUtility.Malloc(64, 16, Allocator.Persistent);
            if (pointer == null)
                throw new InvalidOperationException("Explicit scene unregister probe could not allocate native memory.");

            int id = 0;
            FixedString128Bytes owner = default;
            FixedString128Bytes label = default;
            owner.CopyFromTruncated("MemorySecurityAudit1616");
            label.CopyFromTruncated("ExplicitSceneUnregisterProbe");
            try
            {
                id = NativeMemorySentinel.RegisterPointer(pointer, 64, in owner, in label, NativeAllocationLifetime.Scene, scene);
                if (id <= 0)
                    throw new InvalidOperationException("Explicit scene unregister probe registration failed.");

                if (!NativeMemorySentinel.ContainsTrackedAllocationForDiagnostics(in owner, in label, NativeAllocationLifetime.Scene, scene))
                    throw new InvalidOperationException("Explicit scene registration was not visible to diagnostics.");

                UnsafeUtility.Free(pointer, Allocator.Persistent);
                pointer = null;

                NativeMemorySentinel.Unregister(in owner, in label, scene);
                if (NativeMemorySentinel.ContainsTrackedAllocationForDiagnostics(in owner, in label, NativeAllocationLifetime.Scene, scene))
                    throw new InvalidOperationException("Explicit scene unregister left a tracked allocation behind.");
            }
            finally
            {
                if (pointer != null)
                {
                    UnsafeUtility.Free(pointer, Allocator.Persistent);
                    pointer = null;
                }

                NativeMemorySentinel.Unregister(id);
            }
        }

        private static void RunZeroGcRegistrationProbe()
        {
            ProfilerRecorder recorder = StartGcRecorderCold();
            void* pointer = UnsafeUtility.Malloc(64, 16, Allocator.Persistent);
            if (pointer == null)
                throw new InvalidOperationException("Zero-GC registration probe could not allocate native memory.");

            FixedString128Bytes owner = default;
            FixedString128Bytes label = default;
            owner.CopyFromTruncated("MemorySecurityAudit1616");
            label.CopyFromTruncated("ZeroGcRegisterProbe");
            try
            {
                long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < RegistrationStressIterations; i++)
                {
                    int id = NativeMemorySentinel.RegisterPointer(pointer, 64, in owner, in label, NativeAllocationLifetime.Scene);
                    if (id <= 0)
                        throw new InvalidOperationException("Zero-GC registration probe failed to register native pointer.");

                    NativeMemorySentinel.Unregister(id);
                }

                long afterBytes = GC.GetAllocatedBytesForCurrentThread();
                if (afterBytes != beforeBytes)
                    throw new InvalidOperationException("NativeMemorySentinel fixed-label registration allocated managed bytes: " + (afterBytes - beforeBytes));
            }
            finally
            {
                if (recorder.Valid)
                    recorder.Dispose();
                UnsafeUtility.Free(pointer, Allocator.Persistent);
            }
        }

        private static ProfilerRecorder StartGcRecorderCold()
        {
            for (int i = 0; i < GcAllocCounters.Length; i++)
            {
                try
                {
                    ProfilerRecorder recorder = ProfilerRecorder.StartNew(
                        ProfilerCategory.Memory,
                        GcAllocCounters[i],
                        RecorderCapacity,
                        ProfilerRecorderOptions.Default);
                    if (recorder.Valid)
                        return recorder;
                }
                catch (ArgumentException)
                {
                }
            }

            return default;
        }

        private sealed class HotSwapProbe : IGlobalRegistryHotSwapListener
        {
            public object CurrentService;
            public int RebindCount;

            public void OnGlobalRegistryServiceReplaced(
                GlobalRegistryServiceSlot serviceSlot,
                object previousService,
                object currentService)
            {
                if (serviceSlot != GlobalRegistryServiceSlot.Audio)
                    return;

                CurrentService = currentService;
                RebindCount++;
            }
        }

        private static bool ContainsCachedRegistryAssignment(string body)
        {
            int position = 0;
            while ((position = body.IndexOf("GlobalRegistry.", position, StringComparison.Ordinal)) >= 0)
            {
                int lineStart = body.LastIndexOf('\n', position);
                int lineEnd = body.IndexOf('\n', position);
                if (lineStart < 0)
                    lineStart = 0;
                if (lineEnd < 0)
                    lineEnd = body.Length;

                string line = body.Substring(lineStart, lineEnd - lineStart);
                if (line.IndexOf('=') >= 0 &&
                    line.IndexOf("==", StringComparison.Ordinal) < 0 &&
                    line.IndexOf("!=", StringComparison.Ordinal) < 0 &&
                    (line.IndexOf('_') >= 0 || line.IndexOf("this.", StringComparison.Ordinal) >= 0))
                {
                    return true;
                }

                position += "GlobalRegistry.".Length;
            }

            return false;
        }

        private static string[] ResolveReleaseTokens(string lockToken)
        {
            return lockToken == "TryLockBuffer" ? PinReleaseLockTokens : WriteReleaseLockTokens;
        }

        private static bool HasFinallyReleaseNearLock(string code, int lockIndex, string[] releaseTokens)
        {
            int start = Math.Max(0, lockIndex - 768);
            int length = Math.Min(code.Length - start, 4096);
            string window = code.Substring(start, length);
            int relativeLockIndex = lockIndex - start;
            int tryIndex = window.IndexOf("try", StringComparison.Ordinal);
            int finallyIndex = window.IndexOf("finally", Math.Max(0, relativeLockIndex), StringComparison.Ordinal);
            if (tryIndex < 0 || finallyIndex < 0 || tryIndex > finallyIndex)
                return false;

            return FindNearestMemberInvocation(window, releaseTokens, finallyIndex) >= 0;
        }

        private static bool HasNestedLockBeforeRelease(
            string code,
            int lockIndex,
            string[] lockTokens,
            string[] releaseTokens)
        {
            int releaseIndex = FindNearestMemberInvocation(code, releaseTokens, lockIndex + 1);
            if (releaseIndex < 0)
                return false;

            int nestedLockIndex = FindNearestMemberInvocation(code, lockTokens, lockIndex + 1);
            return nestedLockIndex >= 0 && nestedLockIndex < releaseIndex;
        }

        private static int FindNearestMemberInvocation(string text, string[] tokens, int startIndex)
        {
            int nearest = -1;
            for (int i = 0; i < tokens.Length; i++)
            {
                int index = startIndex;
                while ((index = text.IndexOf(tokens[i], index, StringComparison.Ordinal)) >= 0)
                {
                    if (IsMemberInvocation(text, index, tokens[i]))
                        break;

                    index += tokens[i].Length;
                }

                if (index >= 0 && (nearest < 0 || index < nearest))
                    nearest = index;
            }

            return nearest;
        }

        private static string FindFirstToken(string text, string[] tokens)
        {
            int nearest = -1;
            string selected = string.Empty;
            for (int i = 0; i < tokens.Length; i++)
            {
                int index = text.IndexOf(tokens[i], StringComparison.Ordinal);
                if (index < 0 || (nearest >= 0 && index >= nearest))
                    continue;

                nearest = index;
                selected = tokens[i];
            }

            return selected;
        }

        private static bool TryFindNextClass(string code, ref int position, out int headerStart, out int bodyStart, out int bodyEnd)
        {
            headerStart = -1;
            bodyStart = -1;
            bodyEnd = -1;
            while ((position = IndexOfWord(code, "class", position)) >= 0)
            {
                int openBrace = code.IndexOf(OpenBrace, position);
                if (openBrace < 0)
                    return false;

                int closeBrace = FindMatchingBrace(code, openBrace);
                int currentClass = position;
                position = openBrace + 1;
                if (closeBrace < 0)
                    continue;

                headerStart = currentClass;
                bodyStart = openBrace + 1;
                bodyEnd = closeBrace;
                return true;
            }

            return false;
        }

        private static bool TryFindMethodBody(string code, string methodName, ref int position, out int bodyStart, out int bodyEnd)
        {
            bodyStart = -1;
            bodyEnd = -1;
            string token = methodName + "(";
            while ((position = code.IndexOf(token, position, StringComparison.Ordinal)) >= 0)
            {
                if (position > 0 && IsIdentifier(code[position - 1]))
                {
                    position += token.Length;
                    continue;
                }

                if (!LooksLikeMethodDeclaration(code, position))
                {
                    position += token.Length;
                    continue;
                }

                int closeParenthesis = code.IndexOf(')', position + token.Length);
                if (closeParenthesis < 0)
                    return false;

                int openBrace = code.IndexOf(OpenBrace, closeParenthesis);
                int semicolon = code.IndexOf(';', closeParenthesis);
                if (openBrace < 0 || (semicolon >= 0 && semicolon < openBrace))
                {
                    position = closeParenthesis + 1;
                    continue;
                }

                int closeBrace = FindMatchingBrace(code, openBrace);
                position = openBrace + 1;
                if (closeBrace < 0)
                    continue;

                bodyStart = openBrace + 1;
                bodyEnd = closeBrace;
                return true;
            }

            return false;
        }

        private static bool LooksLikeMethodDeclaration(string code, int methodIndex)
        {
            int previous = PreviousSignificantIndex(code, methodIndex - 1);
            if (previous >= 0 && (code[previous] == '.' || code[previous] == '=' || code[previous] == '>'))
                return false;

            int lineStart = code.LastIndexOf('\n', methodIndex);
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            string prefix = code.Substring(lineStart, methodIndex - lineStart).Trim();
            if (prefix.Length == 0 || prefix.IndexOf('=') >= 0 || prefix.EndsWith(".", StringComparison.Ordinal))
                return false;

            return !IsControlFlowPrefix(prefix);
        }

        private static bool IsControlFlowPrefix(string prefix)
        {
            return prefix.EndsWith("if", StringComparison.Ordinal) ||
                   prefix.EndsWith("while", StringComparison.Ordinal) ||
                   prefix.EndsWith("for", StringComparison.Ordinal) ||
                   prefix.EndsWith("foreach", StringComparison.Ordinal) ||
                   prefix.EndsWith("switch", StringComparison.Ordinal) ||
                   prefix.EndsWith("catch", StringComparison.Ordinal) ||
                   prefix.EndsWith("using", StringComparison.Ordinal) ||
                   prefix.EndsWith("return", StringComparison.Ordinal) ||
                   prefix.EndsWith("throw", StringComparison.Ordinal) ||
                   prefix.EndsWith("new", StringComparison.Ordinal);
        }

        private static bool IsMemberInvocation(string code, int tokenIndex, string token)
        {
            int previous = PreviousSignificantIndex(code, tokenIndex - 1);
            if (previous < 0 || code[previous] != '.')
                return false;

            int next = tokenIndex + token.Length;
            while (next < code.Length && char.IsWhiteSpace(code[next]))
                next++;

            return next < code.Length && (code[next] == '(' || code[next] == '<');
        }

        private static int PreviousSignificantIndex(string text, int start)
        {
            for (int i = start; i >= 0; i--)
            {
                if (!char.IsWhiteSpace(text[i]))
                    return i;
            }

            return -1;
        }

        private static int FindMatchingBrace(string text, int openBrace)
        {
            int depth = 0;
            for (int i = openBrace; i < text.Length; i++)
            {
                if (text[i] == OpenBrace)
                    depth++;
                else if (text[i] == CloseBrace)
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static string StripCommentsAndStrings(string text)
        {
            char[] chars = text.ToCharArray();
            bool lineComment = false;
            bool blockComment = false;
            bool stringLiteral = false;
            bool charLiteral = false;
            bool verbatim = false;
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                char next = i + 1 < chars.Length ? chars[i + 1] : '\0';
                if (lineComment)
                {
                    if (c == '\n')
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
                    else if (c != '\n' && c != '\r')
                    {
                        chars[i] = ' ';
                    }
                    continue;
                }

                if (stringLiteral)
                {
                    if (!verbatim && c == '\\')
                    {
                        chars[i] = ' ';
                        if (i + 1 < chars.Length)
                            chars[++i] = ' ';
                        continue;
                    }

                    if (c == '"' && (!verbatim || next != '"'))
                        stringLiteral = false;
                    else if (c != '\n' && c != '\r')
                        chars[i] = ' ';
                    continue;
                }

                if (charLiteral)
                {
                    if (c == '\\')
                    {
                        chars[i] = ' ';
                        if (i + 1 < chars.Length)
                            chars[++i] = ' ';
                        continue;
                    }

                    if (c == '\'')
                        charLiteral = false;
                    else if (c != '\n' && c != '\r')
                        chars[i] = ' ';
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
                    i++;
                    verbatim = true;
                    stringLiteral = true;
                    continue;
                }

                if (c == '"')
                {
                    verbatim = false;
                    stringLiteral = true;
                    continue;
                }

                if (c == '\'')
                {
                    charLiteral = true;
                }
            }

            return new string(chars);
        }

        private static bool ContainsAny(string text, string[] tokens)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                if (text.IndexOf(tokens[i], StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }

        private static int IndexOfWord(string text, string word, int startIndex)
        {
            int position = startIndex;
            while ((position = text.IndexOf(word, position, StringComparison.Ordinal)) >= 0)
            {
                bool leftOk = position == 0 || !IsIdentifier(text[position - 1]);
                int rightIndex = position + word.Length;
                bool rightOk = rightIndex >= text.Length || !IsIdentifier(text[rightIndex]);
                if (leftOk && rightOk)
                    return position;

                position += word.Length;
            }

            return -1;
        }

        private static bool IsIdentifier(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        private static int CountLineNumber(string text, int index)
        {
            int line = 1;
            int end = Math.Min(index, text.Length);
            for (int i = 0; i < end; i++)
            {
                if (text[i] == '\n')
                    line++;
            }

            return line;
        }

        private static bool IsEditorPath(string path)
        {
            return path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.EndsWith(".Editor.cs", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }

        private static void FeedHash(SHA256 sha, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }

        private static string FinishHash(SHA256 sha)
        {
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            byte[] hash = sha.Hash;
            StringBuilder builder = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                builder.Append(hash[i].ToString("x2"));
            return builder.ToString();
        }

        private static string BuildViolationMessage(List<string> violations, AuditStats stats)
        {
            StringBuilder builder = new StringBuilder(4096);
            builder.Append("FatalArchitectureException: MemorySecurityAudit1616 violations=");
            builder.Append(violations.Count);
            builder.Append(" files=");
            builder.Append(stats.ScriptCount);
            builder.Append(" hash=");
            builder.Append(stats.SourceHash);
            if (violations.Count >= MaxAuditViolationCount)
            {
                builder.Append(" failFastCap=");
                builder.Append(MaxAuditViolationCount);
            }

            int limit = Math.Min(violations.Count, 64);
            for (int i = 0; i < limit; i++)
            {
                builder.Append('\n');
                builder.Append(violations[i]);
            }

            if (violations.Count > limit)
            {
                builder.Append('\n');
                builder.Append("... truncated=");
                builder.Append(violations.Count - limit);
            }

            return builder.ToString();
        }
    }
}
#endif
