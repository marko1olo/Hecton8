using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Hecton8.Core;
using Hecton8.Core.Contracts;

namespace Hecton8.Optimization
{
    /// <summary>
    /// Tracks RenderTexture lifecycle: allocation, usage, disposal.
    /// Detects leaks (RT not disposed within 10s of owner destruction).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-7999)]
    public sealed class RenderTextureLifecycleTracker : MonoBehaviour, IRenderTextureLifecycleService, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        // ── REGISTRY CACHE ─────────────────────────────────────────────────────────
        
        
        // ── PRIVATE STATE ──────────────────────────────────────────────────────────
        
        private bool _registeredSlowTick;
        private bool _registeredLateFrame;
        private bool _registeredService;
        private bool _hotSwapRegistered;
        private bool _leakCheckPending;
        
        // COLD ALLOC: Dictionary<EntityId, RenderTextureAllocationRecord>[256] — RT tracking — owner: RenderTextureLifecycleTracker
        private readonly Dictionary<EntityId, RenderTextureAllocationRecord> _allocations = new Dictionary<EntityId, RenderTextureAllocationRecord>(256);
        
        // COLD ALLOC: List<RenderTextureAllocationRecord>[32] — leak query — owner: RenderTextureLifecycleTracker
        private readonly List<RenderTextureAllocationRecord> _leakQueryResults = new List<RenderTextureAllocationRecord>(32);

        // COLD ALLOC: List<RenderTextureAllocationRecord>[64] — audit report Visor bucket — owner: RenderTextureLifecycleTracker
        private readonly List<RenderTextureAllocationRecord> _reportVisorRTs = new List<RenderTextureAllocationRecord>(64);

        // COLD ALLOC: List<RenderTextureAllocationRecord>[64] — audit report Camera bucket — owner: RenderTextureLifecycleTracker
        private readonly List<RenderTextureAllocationRecord> _reportCameraRTs = new List<RenderTextureAllocationRecord>(64);

        // COLD ALLOC: List<RenderTextureAllocationRecord>[64] — audit report PostFX bucket — owner: RenderTextureLifecycleTracker
        private readonly List<RenderTextureAllocationRecord> _reportPostFXRTs = new List<RenderTextureAllocationRecord>(64);

        // COLD ALLOC: List<RenderTextureAllocationRecord>[64] — audit report UI bucket — owner: RenderTextureLifecycleTracker
        private readonly List<RenderTextureAllocationRecord> _reportUIRTs = new List<RenderTextureAllocationRecord>(64);

        // COLD ALLOC: List<RenderTextureAllocationRecord>[64] — audit report uncategorized bucket — owner: RenderTextureLifecycleTracker
        private readonly List<RenderTextureAllocationRecord> _reportOtherRTs = new List<RenderTextureAllocationRecord>(64);
        
        // COLD ALLOC: StringBuilder[2048] — zero-GC reporting — owner: RenderTextureLifecycleTracker
        private readonly StringBuilder _auditBuilder = new StringBuilder(2048);
        
        // ── PUBLIC PROPERTIES ──────────────────────────────────────────────────────
        
        /// <summary>
        /// Returns total number of tracked RenderTextures.
        /// </summary>
        public int TrackedRenderTextureCount => _allocations.Count;
        
        /// <summary>
        /// Returns total memory consumed by tracked RenderTextures in bytes.
        /// </summary>
        public long TrackedRenderTextureMemoryBytes
        {
            get
            {
                long total = 0;
                Dictionary<EntityId, RenderTextureAllocationRecord>.Enumerator enumerator = _allocations.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    RenderTextureAllocationRecord record = enumerator.Current.Value;
                    if (!record.IsDisposed)
                        total += record.MemoryBytes;
                }
                return total;
            }
        }
        
        // ── LIFECYCLE ──────────────────────────────────────────────────────────────
        
        private void OnEnable()
        {
            if (TryRegisterService())
            {
                TryRegisterHotSwapListener();
                TryRegister();
            }
        }
        
        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
        }
        
        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher ||
                currentService == null ||
                !_registeredService ||
                !isActiveAndEnabled)
            {
                return;
            }

            TryUnregister();
            TryRegister();
        }
        
        // ── ISLOWTICABLE ───────────────────────────────────────────────────────────
        
        /// <summary>
        /// ISlowTickable implementation. Checks for leaks every ~0.5s.
        /// </summary>
        public void SlowTick()
        {
            _leakCheckPending = true;
        }

        public void LateFrameTick()
        {
            if (!_leakCheckPending)
                return;

            _leakCheckPending = false;
            CheckForLeaks();
        }
        
        // ── PUBLIC API ─────────────────────────────────────────────────────────────
        
        /// <summary>
        /// Registers a RenderTexture allocation with owner component.
        /// </summary>
        /// <param name="rt">RenderTexture instance.</param>
        /// <param name="owner">Owner component (MonoBehaviour).</param>
        /// <param name="allocationStackTrace">Optional stack trace for leak debugging.</param>
        public void RegisterAllocation(RenderTexture rt, Component owner, string allocationStackTrace = null)
        {
            if (rt == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[LifecycleTracker] RegisterAllocation called with null RenderTexture");
#endif
                return;
            }
            
            if (owner == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[LifecycleTracker] RegisterAllocation called with null owner");
#endif
                return;
            }
            
            EntityId instanceID = rt.GetEntityId();
            
            if (_allocations.ContainsKey(instanceID))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning(
                    "[LifecycleTracker] Duplicate registration for RT " +
                    rt.name +
                    " (ID: " +
                    instanceID +
                    "). Updating existing record.");
#endif
                // Update existing record
                var existing = _allocations[instanceID];
                existing.Owner = owner;
                existing.OwnerCategory = ClassifyOwner(owner);
                existing.AllocationTime = ResolveLifecycleClockSeconds();
                existing.AllocationStackTrace = allocationStackTrace;
                existing.IsDisposed = false;
                _allocations[instanceID] = existing;
                return;
            }
            
            var record = new RenderTextureAllocationRecord
            {
                RenderTexture = rt,
                Owner = owner,
                OwnerCategory = ClassifyOwner(owner),
                Width = rt.width,
                Height = rt.height,
                Format = rt.format,
                AllocationTime = ResolveLifecycleClockSeconds(),
                AllocationStackTrace = allocationStackTrace,
                IsDisposed = false
            };
            
            _allocations[instanceID] = record;
        }
        
        /// <summary>
        /// Registers a RenderTexture disposal.
        /// </summary>
        /// <param name="rt">RenderTexture instance.</param>
        public void RegisterDisposal(RenderTexture rt)
        {
            if (rt == null)
                return;
            
            EntityId instanceID = rt.GetEntityId();
            
            if (_allocations.TryGetValue(instanceID, out var record))
            {
                record.IsDisposed = true;
                _allocations[instanceID] = record;
            }
        }
        
        /// <summary>
        /// Generates audit report grouped by owner (Visor, Camera, PostFX, UI).
        /// </summary>
        /// <param name="reportBuilder">Pre-allocated StringBuilder for zero-GC reporting.</param>
        public void GenerateAuditReport(StringBuilder reportBuilder)
        {
            reportBuilder.Clear();
            reportBuilder.AppendLine("=== RenderTexture Lifecycle Audit ===");
            reportBuilder.Append("Total Tracked: ").Append(TrackedRenderTextureCount).AppendLine();
            reportBuilder.Append("Total Memory: ").Append((TrackedRenderTextureMemoryBytes / (1024f * 1024f)).ToString("0.00")).AppendLine(" MB");
            reportBuilder.AppendLine();

            ClearReportBuckets();
            
            Dictionary<EntityId, RenderTextureAllocationRecord>.Enumerator enumerator = _allocations.GetEnumerator();
            while (enumerator.MoveNext())
            {
                RenderTextureAllocationRecord record = enumerator.Current.Value;
                if (record.IsDisposed)
                    continue;

                switch (record.OwnerCategory)
                {
                    case RenderTextureOwnerCategory.Visor:
                        _reportVisorRTs.Add(record);
                        break;

                    case RenderTextureOwnerCategory.Camera:
                        _reportCameraRTs.Add(record);
                        break;

                    case RenderTextureOwnerCategory.PostFX:
                        _reportPostFXRTs.Add(record);
                        break;

                    case RenderTextureOwnerCategory.UI:
                        _reportUIRTs.Add(record);
                        break;

                    default:
                        _reportOtherRTs.Add(record);
                        break;
                }
            }
            
            AppendCategoryReport(reportBuilder, "Visor", _reportVisorRTs);
            AppendCategoryReport(reportBuilder, "Camera", _reportCameraRTs);
            AppendCategoryReport(reportBuilder, "PostFX", _reportPostFXRTs);
            AppendCategoryReport(reportBuilder, "UI", _reportUIRTs);
            AppendCategoryReport(reportBuilder, "Other", _reportOtherRTs);
        }
        
        /// <summary>
        /// Returns list of leaked RenderTextures (owner destroyed but RT not disposed).
        /// </summary>
        /// <param name="results">Pre-allocated list for zero-GC query.</param>
        public void GetLeakedRenderTextures(List<RenderTextureAllocationRecord> results)
        {
            results.Clear();
            float now = ResolveLifecycleClockSeconds();
            
            Dictionary<EntityId, RenderTextureAllocationRecord>.Enumerator enumerator = _allocations.GetEnumerator();
            while (enumerator.MoveNext())
            {
                RenderTextureAllocationRecord record = enumerator.Current.Value;
                if (record.Owner == null && !record.IsDisposed && now - record.AllocationTime > 10f)
                {
                    results.Add(record);
                }
            }
        }
        
        /// <summary>
        /// Returns list of RenderTextures filtered by owner category.
        /// Zero-GC: clears results list, iterates allocations, filters by owner type name.
        /// </summary>
        /// <param name="category">Category name: "Visor", "Camera", "PostFX", "UI", "Other".</param>
        /// <param name="results">Pre-allocated list for zero-GC query.</param>
        public void GetAllocationsByCategory(string category, List<RenderTextureAllocationRecord> results)
        {
            GetAllocationsByCategory(ResolveCategory(category), results);
        }

        /// <summary>
        /// Returns list of RenderTextures filtered by cached owner category.
        /// </summary>
        /// <param name="category">Cached owner category.</param>
        /// <param name="results">Pre-allocated list for zero-GC query.</param>
        public void GetAllocationsByCategory(RenderTextureOwnerCategory category, List<RenderTextureAllocationRecord> results)
        {
            results.Clear();

            Dictionary<EntityId, RenderTextureAllocationRecord>.Enumerator enumerator = _allocations.GetEnumerator();
            while (enumerator.MoveNext())
            {
                RenderTextureAllocationRecord record = enumerator.Current.Value;
                if (record.IsDisposed || record.OwnerCategory != category)
                    continue;

                results.Add(record);
            }
        }
        
        // ── PRIVATE METHODS ────────────────────────────────────────────────────────
        
        private void TryRegister()
        {
            if (_registeredSlowTick)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;
            if (!ReferenceEquals(GlobalRegistry.RenderTextureLifecycle, this))
                return;

            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core);
            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);
        }

        private bool TryRegisterService()
        {
            if (_registeredService)
                return true;
            if (!Application.isPlaying)
                return false;

            RenderTextureLifecycleTracker registered = GlobalRegistry.RenderTextureLifecycle;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return false;
            }

            GlobalRegistry.RegisterRenderTextureLifecycleRuntime(this);
            _registeredService = ReferenceEquals(GlobalRegistry.RenderTextureLifecycle, this);
            return _registeredService;
        }

        private void TryUnregister()
        {
            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
                _registeredSlowTick = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
                _registeredLateFrame = false;
            }

            _leakCheckPending = false;
        }

        private void TryUnregisterService()
        {
            if (!_registeredService)
                return;

            GlobalRegistry.UnregisterRenderTextureLifecycleRuntime(this);
            _registeredService = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void CheckForLeaks()
        {
            _leakQueryResults.Clear();
            GetLeakedRenderTextures(_leakQueryResults);
            
            if (_leakQueryResults.Count > 0)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                for (int i = 0; i < _leakQueryResults.Count; i++)
                {
                    RenderTextureAllocationRecord leak = _leakQueryResults[i];
                    Hecton8.Core.H8Debug.LogError(
                        "[LifecycleTracker] RT LEAK DETECTED: " +
                        leak.RenderTexture.name +
                        " (" +
                        leak.Width +
                        "x" +
                        leak.Height +
                        " " +
                        leak.Format +
                        ") - Owner destroyed but RT not disposed. Allocation time: " +
                        leak.AllocationTime.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                        "s\n" +
                        leak.AllocationStackTrace);
                }
#endif
            }
        }

        private static float ResolveLifecycleClockSeconds()
        {
            if (!Application.isPlaying)
                return 0f;

            SystemDispatcher dispatcher = SystemDispatcher.ActiveRuntimeInstance;
            if (dispatcher == null)
                return 0f;

            double timeSeconds = dispatcher.UnscaledTimeSeconds;
            if (!Unity.Mathematics.math.isfinite(timeSeconds) || timeSeconds <= 0d)
                return 0f;

            return (float)Unity.Mathematics.math.min(timeSeconds, 86400d);
        }

        private void ClearReportBuckets()
        {
            _reportVisorRTs.Clear();
            _reportCameraRTs.Clear();
            _reportPostFXRTs.Clear();
            _reportUIRTs.Clear();
            _reportOtherRTs.Clear();
        }
        
        private void AppendCategoryReport(StringBuilder builder, string category, List<RenderTextureAllocationRecord> records)
        {
            if (records.Count == 0)
                return;
            
            long totalMemory = 0;
            for (int i = 0; i < records.Count; i++)
            {
                RenderTextureAllocationRecord record = records[i];
                totalMemory += record.MemoryBytes;
            }
            
            builder.Append("--- ").Append(category).Append(" (").Append(records.Count).Append(" RTs, ")
                   .Append((totalMemory / (1024f * 1024f)).ToString("0.00")).AppendLine(" MB) ---");
            
            for (int i = 0; i < records.Count; i++)
            {
                RenderTextureAllocationRecord record = records[i];
                builder.Append("  ").Append(record.RenderTexture.name).Append(" (")
                       .Append(record.Width).Append("x").Append(record.Height).Append(" ")
                       .Append(record.Format).Append(", ")
                       .Append((record.MemoryBytes / (1024f * 1024f)).ToString("0.00")).Append(" MB) - Owner: ")
                       .AppendLine(record.Owner != null ? record.Owner.name : "NULL");
            }
            
            builder.AppendLine();
        }

        private static RenderTextureOwnerCategory ResolveCategory(string category)
        {
            switch (category)
            {
                case "Visor":
                    return RenderTextureOwnerCategory.Visor;

                case "Camera":
                    return RenderTextureOwnerCategory.Camera;

                case "PostFX":
                    return RenderTextureOwnerCategory.PostFX;

                case "UI":
                    return RenderTextureOwnerCategory.UI;

                default:
                    return RenderTextureOwnerCategory.Other;
            }
        }

        private static RenderTextureOwnerCategory ClassifyOwner(Component owner)
        {
            if (owner == null)
                return RenderTextureOwnerCategory.Other;

            string ownerTypeName = owner.GetType().Name;
            if (ownerTypeName.Contains("Visor", System.StringComparison.Ordinal) ||
                ownerTypeName.Contains("HUD", System.StringComparison.Ordinal))
                return RenderTextureOwnerCategory.Visor;
            if (ownerTypeName.Contains("Camera", System.StringComparison.Ordinal))
                return RenderTextureOwnerCategory.Camera;
            if (ownerTypeName.Contains("PostFX", System.StringComparison.Ordinal) ||
                ownerTypeName.Contains("Volume", System.StringComparison.Ordinal))
                return RenderTextureOwnerCategory.PostFX;
            if (ownerTypeName.Contains("UI", System.StringComparison.Ordinal) ||
                ownerTypeName.Contains("Canvas", System.StringComparison.Ordinal))
                return RenderTextureOwnerCategory.UI;

            return RenderTextureOwnerCategory.Other;
        }
    }
}
