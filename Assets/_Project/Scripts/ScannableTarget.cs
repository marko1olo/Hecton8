using UnityEngine;
using Hecton8.Data;
using Hecton8.World;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Scannable Target")]
    public sealed class ScannableTarget : MonoBehaviour
    {
        [SerializeField] private string entryId = "scannable.unknown";
        [SerializeField] private string entryTitle = "UNIDENTIFIED CONTACT";
        [SerializeField] private string entryCategory = "Unknown";
        [TextArea(2, 5)]
        [SerializeField] private string entrySummary =
            "Passive scan profile has been captured. Manual classification pending.";
        private int _spatialHandle;
        private string _resolvedEntryId;
        private string _resolvedEntryTitle;
        private string _resolvedEntryCategory;
        private string _resolvedEntrySummary;
        private uint _entityHash;

        public string EntryId
        {
            get
            {
                EnsureResolvedStrings();
                return _resolvedEntryId;
            }
        }

        public string EntryTitle
        {
            get
            {
                EnsureResolvedStrings();
                return _resolvedEntryTitle;
            }
        }

        public string EntryCategory
        {
            get
            {
                EnsureResolvedStrings();
                return _resolvedEntryCategory;
            }
        }

        public string EntrySummary
        {
            get
            {
                EnsureResolvedStrings();
                return _resolvedEntrySummary;
            }
        }

        /// <summary>Stable FNV-1a entity hash used by zero-GC scanner paths.</summary>
        public uint EntityHash
        {
            get
            {
                EnsureResolvedStrings();
                return _entityHash;
            }
        }

        /// <summary>Signed form of <see cref="EntityHash"/> for native hash maps keyed by int.</summary>
        public int EntityHash32
        {
            get
            {
                EnsureResolvedStrings();
                return unchecked((int)_entityHash);
            }
        }

        public void Configure(string id, string title, string category, string summary)
        {
            entryId = string.IsNullOrWhiteSpace(id) ? gameObject.name : id.Trim();
            entryTitle = string.IsNullOrWhiteSpace(title) ? CachedToUpperInvariant(gameObject.name) : title.Trim();
            entryCategory = string.IsNullOrWhiteSpace(category) ? "Unknown" : category.Trim();
            entrySummary = string.IsNullOrWhiteSpace(summary)
                ? "Passive scan profile has been captured."
                : summary.Trim();
            RefreshResolvedStrings();
        }

        private void Awake()
        {
            RefreshResolvedStrings();
        }

        private void OnEnable()
        {
            EnsureResolvedStrings();
            if (_spatialHandle == 0)
                _spatialHandle = WorldSpatialHashGrid.RegisterScannable(this);
        }

        private void OnDisable()
        {
            if (_spatialHandle == 0)
                return;

            WorldSpatialHashGrid.Unregister(_spatialHandle);
            _spatialHandle = 0;
        }

        private void OnDestroy()
        {
            if (_spatialHandle == 0)
                return;

            WorldSpatialHashGrid.Unregister(_spatialHandle);
            _spatialHandle = 0;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (string.IsNullOrWhiteSpace(entryId))
                entryId = gameObject.name.Trim().ToLowerInvariant().Replace(' ', '_');

            if (string.IsNullOrWhiteSpace(entryTitle))
                entryTitle = CachedToUpperInvariant(gameObject.name);

            if (string.IsNullOrWhiteSpace(entryCategory))
                entryCategory = "Unknown";

            RefreshResolvedStrings();
        }
#endif

        // ══════════════════════════════════════════════════════════
        //  ZERO-GC STRING CACHING
        // ══════════════════════════════════════════════════════════

        private static readonly string[] _upperCacheKeys = new string[16]; // COLD ALLOC: string[16] - uppercase fallback key cache - owner: ScannableTarget
        private static readonly string[] _upperCacheValues = new string[16]; // COLD ALLOC: string[16] - uppercase fallback value cache - owner: ScannableTarget

        private void EnsureResolvedStrings()
        {
            if (_resolvedEntryId == null)
                RefreshResolvedStrings();
        }

        private void RefreshResolvedStrings()
        {
            string objectName = gameObject.name;
            _resolvedEntryId = string.IsNullOrWhiteSpace(entryId) ? objectName : entryId.Trim();
            _resolvedEntryTitle = string.IsNullOrWhiteSpace(entryTitle) ? CachedToUpperInvariant(objectName) : entryTitle.Trim();
            _resolvedEntryCategory = string.IsNullOrWhiteSpace(entryCategory) ? "Unknown" : entryCategory.Trim();
            _resolvedEntrySummary = string.IsNullOrWhiteSpace(entrySummary)
                ? "Passive scan profile has been captured."
                : entrySummary.Trim();
            _entityHash = H8DataHash.ComputeFnv1A32(_resolvedEntryId);
        }

        private static string CachedToUpperInvariant(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            int hash = input.GetHashCode() & 0xF;
            string cachedKey = _upperCacheKeys[hash];
            if (cachedKey != null && string.Equals(cachedKey, input, System.StringComparison.Ordinal))
                return _upperCacheValues[hash];

            string upper = input.ToUpperInvariant();
            _upperCacheKeys[hash] = input;
            _upperCacheValues[hash] = upper;
            return upper;
        }
    }
}
