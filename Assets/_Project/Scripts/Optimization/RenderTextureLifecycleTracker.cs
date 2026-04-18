using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Optimization
{
    /// <summary>
    /// Tracks RenderTexture lifecycle: allocation, usage, disposal.
    /// Detects leaks (RT not disposed within 10s of owner destruction).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-7999)]
    public sealed class RenderTextureLifecycleTracker : MonoBehaviour, ISlowTickable
    {
        // ── SINGLETON ──────────────────────────────────────────────────────────────
        
        private static RenderTextureLifecycleTracker _instance;
        
        /// <summary>
        /// Singleton instance. Null-check required in OnDestroy.
        /// </summary>
        public static RenderTextureLifecycleTracker Instance => _instance;
        
        // ── PRIVATE STATE ──────────────────────────────────────────────────────────
        
        private bool _registeredSlowTick;
        
        // COLD ALLOC: Dictionary<EntityId, RenderTextureAllocationRecord>[256] — RT tracking — owner: LifecycleTracker
        private readonly Dictionary<EntityId, RenderTextureAllocationRecord> _allocations = new Dictionary<EntityId, RenderTextureAllocationRecord>(256);
        
        // COLD ALLOC: List<RenderTextureAllocationRecord>[32] — leak query — owner: LifecycleTracker
        private readonly List<RenderTextureAllocationRecord> _leakQueryResults = new List<RenderTextureAllocationRecord>(32);
        
        // COLD ALLOC: StringBuilder[2048] — zero-GC reporting — owner: LifecycleTracker
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
                foreach (var kvp in _allocations)
                {
                    if (!kvp.Value.IsDisposed)
                        total += kvp.Value.MemoryBytes;
                }
                return total;
            }
        }
        
        // ── LIFECYCLE ──────────────────────────────────────────────────────────────
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
        }
        
        private void OnEnable()
        {
            TryRegister();
        }
        
        private void OnDisable()
        {
            TryUnregister();
        }
        
        private void OnDestroy()
        {
            TryUnregister();

            if (_instance == this)
                _instance = null;
        }
        
        // ── ISLOWTICABLE ───────────────────────────────────────────────────────────
        
        /// <summary>
        /// ISlowTickable implementation. Checks for leaks every ~0.5s.
        /// </summary>
        public void SlowTick()
        {
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
                Debug.LogError("[LifecycleTracker] RegisterAllocation called with null RenderTexture");
#endif
                return;
            }
            
            if (owner == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[LifecycleTracker] RegisterAllocation called with null owner");
#endif
                return;
            }
            
            EntityId instanceID = rt.GetEntityId();
            
            if (_allocations.ContainsKey(instanceID))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[LifecycleTracker] Duplicate registration for RT {rt.name} (ID: {instanceID}). Updating existing record.");
#endif
                // Update existing record
                var existing = _allocations[instanceID];
                existing.Owner = owner;
                existing.AllocationTime = Time.time;
                existing.AllocationStackTrace = allocationStackTrace;
                existing.IsDisposed = false;
                _allocations[instanceID] = existing;
                return;
            }
            
            var record = new RenderTextureAllocationRecord
            {
                RenderTexture = rt,
                Owner = owner,
                Width = rt.width,
                Height = rt.height,
                Format = rt.format,
                AllocationTime = Time.time,
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
            
            // Group by owner type
            var visorRTs = new List<RenderTextureAllocationRecord>();
            var cameraRTs = new List<RenderTextureAllocationRecord>();
            var postFXRTs = new List<RenderTextureAllocationRecord>();
            var uiRTs = new List<RenderTextureAllocationRecord>();
            var otherRTs = new List<RenderTextureAllocationRecord>();
            
            foreach (var kvp in _allocations)
            {
                if (kvp.Value.IsDisposed)
                    continue;
                
                var ownerName = kvp.Value.Owner != null ? kvp.Value.Owner.GetType().Name : "Unknown";
                
                if (ownerName.Contains("Visor") || ownerName.Contains("HUD"))
                    visorRTs.Add(kvp.Value);
                else if (ownerName.Contains("Camera"))
                    cameraRTs.Add(kvp.Value);
                else if (ownerName.Contains("PostFX") || ownerName.Contains("Volume"))
                    postFXRTs.Add(kvp.Value);
                else if (ownerName.Contains("UI") || ownerName.Contains("Canvas"))
                    uiRTs.Add(kvp.Value);
                else
                    otherRTs.Add(kvp.Value);
            }
            
            AppendCategoryReport(reportBuilder, "Visor", visorRTs);
            AppendCategoryReport(reportBuilder, "Camera", cameraRTs);
            AppendCategoryReport(reportBuilder, "PostFX", postFXRTs);
            AppendCategoryReport(reportBuilder, "UI", uiRTs);
            AppendCategoryReport(reportBuilder, "Other", otherRTs);
        }
        
        /// <summary>
        /// Returns list of leaked RenderTextures (owner destroyed but RT not disposed).
        /// </summary>
        /// <param name="results">Pre-allocated list for zero-GC query.</param>
        public void GetLeakedRenderTextures(List<RenderTextureAllocationRecord> results)
        {
            results.Clear();
            
            foreach (var kvp in _allocations)
            {
                if (kvp.Value.Owner == null && !kvp.Value.IsDisposed && Time.time - kvp.Value.AllocationTime > 10f)
                {
                    results.Add(kvp.Value);
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
            results.Clear();
            
            foreach (var kvp in _allocations)
            {
                if (kvp.Value.IsDisposed)
                    continue;
                
                var ownerName = kvp.Value.Owner != null ? kvp.Value.Owner.GetType().Name : "Unknown";
                
                bool matches = false;
                
                switch (category)
                {
                    case "Visor":
                        matches = ownerName.Contains("Visor") || ownerName.Contains("HUD");
                        break;
                    case "Camera":
                        matches = ownerName.Contains("Camera");
                        break;
                    case "PostFX":
                        matches = ownerName.Contains("PostFX") || ownerName.Contains("Volume");
                        break;
                    case "UI":
                        matches = ownerName.Contains("UI") || ownerName.Contains("Canvas");
                        break;
                    case "Other":
                        matches = !ownerName.Contains("Visor") && !ownerName.Contains("HUD") &&
                                  !ownerName.Contains("Camera") &&
                                  !ownerName.Contains("PostFX") && !ownerName.Contains("Volume") &&
                                  !ownerName.Contains("UI") && !ownerName.Contains("Canvas");
                        break;
                }
                
                if (matches)
                    results.Add(kvp.Value);
            }
        }
        
        // ── PRIVATE METHODS ────────────────────────────────────────────────────────
        
        private void TryRegister()
        {
            if (_registeredSlowTick)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register((ISlowTickable)this);
            _registeredSlowTick = true;
        }

        private void TryUnregister()
        {
            if (!_registeredSlowTick)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
            {
                tickManager.Unregister((ISlowTickable)this);
            }

            _registeredSlowTick = false;
        }

        private void CheckForLeaks()
        {
            _leakQueryResults.Clear();
            GetLeakedRenderTextures(_leakQueryResults);
            
            if (_leakQueryResults.Count > 0)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                foreach (var leak in _leakQueryResults)
                {
                    Debug.LogError($"[LifecycleTracker] RT LEAK DETECTED: {leak.RenderTexture.name} ({leak.Width}x{leak.Height} {leak.Format}) - Owner destroyed but RT not disposed. Allocation time: {leak.AllocationTime:F2}s\n{leak.AllocationStackTrace}");
                }
#endif
            }
        }
        
        private void AppendCategoryReport(StringBuilder builder, string category, List<RenderTextureAllocationRecord> records)
        {
            if (records.Count == 0)
                return;
            
            long totalMemory = 0;
            foreach (var record in records)
                totalMemory += record.MemoryBytes;
            
            builder.Append("--- ").Append(category).Append(" (").Append(records.Count).Append(" RTs, ")
                   .Append((totalMemory / (1024f * 1024f)).ToString("0.00")).AppendLine(" MB) ---");
            
            foreach (var record in records)
            {
                builder.Append("  ").Append(record.RenderTexture.name).Append(" (")
                       .Append(record.Width).Append("x").Append(record.Height).Append(" ")
                       .Append(record.Format).Append(", ")
                       .Append((record.MemoryBytes / (1024f * 1024f)).ToString("0.00")).Append(" MB) - Owner: ")
                       .AppendLine(record.Owner != null ? record.Owner.name : "NULL");
            }
            
            builder.AppendLine();
        }
    }
}
