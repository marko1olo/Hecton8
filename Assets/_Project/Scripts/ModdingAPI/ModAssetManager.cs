using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Hecton8.Modding
{
    /// <summary>
    /// Runtime owner for mod content assets loaded from on-disk AssetBundles and selected raw file fallbacks.
    /// Bundle loading is cold-path only and cached for the process lifetime.
    /// </summary>
    internal static class ModAssetManager
    {
        // COLD ALLOC: Dictionary<string,string>[32] — modId to bundle path lookup — owner: ModAssetManager
        private static readonly Dictionary<string, string> _bundlePaths = new Dictionary<string, string>(32);
        // COLD ALLOC: Dictionary<string,AssetBundle>[32] — cached loaded mod bundles — owner: ModAssetManager
        private static readonly Dictionary<string, AssetBundle> _loadedBundles = new Dictionary<string, AssetBundle>(32);
        // COLD ALLOC: Dictionary<string,Texture2D>[32] — cached raw PNG textures — owner: ModAssetManager
        private static readonly Dictionary<string, Texture2D> _rawTextures = new Dictionary<string, Texture2D>(32);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            UnloadAllBundles();
            _bundlePaths.Clear();
            _rawTextures.Clear();
        }

        /// <summary>
        /// Registers the primary AssetBundle path resolved for a mod package.
        /// </summary>
        /// <param name="modId">Stable mod identifier.</param>
        /// <param name="bundlePath">Absolute bundle path or an empty value when no bundle exists.</param>
        internal static void RegisterBundlePath(string modId, string bundlePath)
        {
            if (string.IsNullOrWhiteSpace(modId))
                return;

            if (string.IsNullOrWhiteSpace(bundlePath))
            {
                _bundlePaths.Remove(modId);
                return;
            }

            _bundlePaths[modId] = bundlePath;
        }

        /// <summary>
        /// Loads a prefab from the mod's registered AssetBundle.
        /// </summary>
        internal static GameObject LoadPrefab(string modId, string assetName)
        {
            return LoadAsset<GameObject>(modId, assetName);
        }

        /// <summary>
        /// Loads an audio clip from the mod's registered AssetBundle.
        /// </summary>
        internal static AudioClip LoadAudioClip(string modId, string assetName)
        {
            return LoadAsset<AudioClip>(modId, assetName);
        }

        /// <summary>
        /// Loads a texture from the mod's registered AssetBundle.
        /// Falls back to raw PNG disk loading when the requested asset name points to a file inside the mod directory.
        /// </summary>
        internal static Texture2D LoadTexture(string modId, string assetName)
        {
            Texture2D texture = LoadAsset<Texture2D>(modId, assetName);
            if (texture != null)
                return texture;

            return LoadRawTexture(modId, assetName);
        }

        private static TAsset LoadAsset<TAsset>(string modId, string assetName)
            where TAsset : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(modId) || string.IsNullOrWhiteSpace(assetName))
                return null;

            if (!TryGetLoadedBundle(modId, out AssetBundle bundle))
                return null;

            TAsset asset = bundle.LoadAsset<TAsset>(assetName);
            if (asset != null)
                return asset;

            string[] assetNames = bundle.GetAllAssetNames();
            if (assetNames == null || assetNames.Length == 0)
                return null;

            string normalizedName = assetName.Replace('\\', '/').ToLowerInvariant();
            for (int i = 0; i < assetNames.Length; i++)
            {
                string candidate = assetNames[i];
                if (candidate == null)
                    continue;

                string normalizedCandidate = candidate.ToLowerInvariant();
                if (!normalizedCandidate.EndsWith(normalizedName))
                    continue;

                asset = bundle.LoadAsset<TAsset>(candidate);
                if (asset != null)
                    return asset;
            }

            Debug.LogWarning($"[ModAssetManager] Asset '{assetName}' was not found in bundle for mod '{modId}'.");
            return null;
        }

        private static bool TryGetLoadedBundle(string modId, out AssetBundle bundle)
        {
            if (_loadedBundles.TryGetValue(modId, out bundle) && bundle != null)
                return true;

            bundle = null;
            if (!_bundlePaths.TryGetValue(modId, out string bundlePath) ||
                string.IsNullOrWhiteSpace(bundlePath) ||
                !File.Exists(bundlePath))
            {
                return false;
            }

            bundle = AssetBundle.LoadFromFile(bundlePath);
            if (bundle == null)
            {
                Debug.LogWarning($"[ModAssetManager] Failed to load AssetBundle '{bundlePath}' for mod '{modId}'.");
                return false;
            }

            _loadedBundles[modId] = bundle;
            return true;
        }

        private static Texture2D LoadRawTexture(string modId, string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName) || !assetName.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                return null;

            if (!ModLoader.TryGetModDirectory(modId, out string modDirectory) || string.IsNullOrWhiteSpace(modDirectory))
                return null;

            string normalizedRelativePath = assetName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            string filePath = Path.Combine(modDirectory, normalizedRelativePath);
            if (!File.Exists(filePath))
                return null;

            string cacheKey = modId + "|" + filePath;
            if (_rawTextures.TryGetValue(cacheKey, out Texture2D cachedTexture) && cachedTexture != null)
                return cachedTexture;

            byte[] pngBytes;
            try
            {
                pngBytes = File.ReadAllBytes(filePath);
            }
            catch (IOException exception)
            {
                Debug.LogWarning($"[ModAssetManager] Failed to read raw texture '{filePath}': {exception.Message}");
                return null;
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, false)
            {
                name = Path.GetFileNameWithoutExtension(filePath)
            }; // COLD ALLOC: Texture2D[1] — raw PNG fallback for mod texture loading — owner: ModAssetManager

            if (!ImageConversion.LoadImage(texture, pngBytes, false))
            {
                UnityEngine.Object.Destroy(texture);
                Debug.LogWarning($"[ModAssetManager] PNG decode failed for '{filePath}'.");
                return null;
            }

            _rawTextures[cacheKey] = texture;
            return texture;
        }

        private static void UnloadAllBundles()
        {
            Dictionary<string, AssetBundle>.Enumerator enumerator = _loadedBundles.GetEnumerator();
            while (enumerator.MoveNext())
            {
                AssetBundle bundle = enumerator.Current.Value;
                if (bundle != null)
                    bundle.Unload(false);
            }

            _loadedBundles.Clear();

            Dictionary<string, Texture2D>.Enumerator textureEnumerator = _rawTextures.GetEnumerator();
            while (textureEnumerator.MoveNext())
            {
                Texture2D texture = textureEnumerator.Current.Value;
                if (texture != null)
                    UnityEngine.Object.Destroy(texture);
            }

            _rawTextures.Clear();
        }
    }
}
