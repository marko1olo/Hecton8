using Hecton.Localization;
using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.UI
{
    /// <summary>
    /// Registry node attached to UI TMP text components so font streaming can avoid global scene scans.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class HectonTextNode : MonoBehaviour
    {
        [Header("── Localization Registry ──────────────────")]
        [Tooltip("Deterministic hierarchy hash baked in the editor. Runtime-created texts fall back to an instance hash.")]
        [SerializeField, HideInInspector] private int _bakedHierarchyHash;

        [Tooltip("Optional zero-GC localization key hash for staged language refreshes.")]
        [SerializeField] private int _localizationKeyHash;

        [Tooltip("Runtime localization residency layer for this text owner.")]
        [SerializeField] private LocLayer _layer = LocLayer.Core;

        [Tooltip("True when this TMP owner is user-input driven and must not be overwritten by staged localization refreshes.")]
        [SerializeField] private bool _isUserInput;

        private TMP_Text _text;

        /// <summary>Cached TMP text component.</summary>
        public TMP_Text TextComponent => _text;

        /// <summary>Deterministic hierarchy hash for registry lookup.</summary>
        public int HierarchyHash => _bakedHierarchyHash;

        /// <summary>Optional zero-GC localization key hash.</summary>
        public int LocalizationKeyHash => _localizationKeyHash;

        /// <summary>Localization residency layer.</summary>
        public LocLayer Layer => _layer;

        /// <summary>True when this text is user-authored input.</summary>
        public bool IsUserInput => _isUserInput;

        internal int RegistryIndex { get; set; } = -1;

        private void Awake()
        {
            CacheTextComponent();
            EnsureRuntimeHierarchyHash();
            if (isActiveAndEnabled)
                TMP_TextRegistry.Register(this);
        }

        private void OnEnable()
        {
            CacheTextComponent();
            EnsureRuntimeHierarchyHash();
            TMP_TextRegistry.Register(this);
        }

        private void OnDisable()
        {
            TMP_TextRegistry.Unregister(this);
        }

        /// <summary>
        /// Update zero-GC localization metadata for runtime-created TMP owners.
        /// </summary>
        public void SetMetadata(int localizationKeyHash, LocLayer layer, bool isUserInput)
        {
            _localizationKeyHash = localizationKeyHash;
            _layer = layer;
            _isUserInput = isUserInput;
            TMP_TextRegistry.Refresh(this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            CacheTextComponent();
            int bakedHash = ComputeEditorHierarchyHash(transform);
            if (_bakedHierarchyHash == bakedHash)
                return;

            _bakedHierarchyHash = bakedHash;
            EditorUtility.SetDirty(this);
        }
#endif

        private void CacheTextComponent()
        {
            if (_text == null)
                _text = GetComponent<TMP_Text>();
        }

        private void EnsureRuntimeHierarchyHash()
        {
            if (_bakedHierarchyHash != 0)
                return;

            unchecked
            {
                _bakedHierarchyHash = 0x40000000 | (gameObject.GetHashCode() & 0x3FFFFFFF);
            }
        }

#if UNITY_EDITOR
        private static int ComputeEditorHierarchyHash(Transform target)
        {
            ulong hash = 14695981039346656037UL;
            AppendHierarchyHash(ref hash, target);
            return unchecked((int)(hash ^ (hash >> 32)));
        }

        private static void AppendHierarchyHash(ref ulong hash, Transform current)
        {
            if (current == null)
                return;

            Transform parent = current.parent;
            if (parent != null)
                AppendHierarchyHash(ref hash, parent);

            hash = HashByte(hash, (byte)'/');
            string name = current.name;
            for (int i = 0; i < name.Length; i++)
            {
                char symbol = name[i];
                hash = HashByte(hash, (byte)symbol);
                hash = HashByte(hash, (byte)(symbol >> 8));
            }
        }

        private static ulong HashByte(ulong hash, byte value)
        {
            return (hash ^ value) * 1099511628211UL;
        }
#endif
    }
}
