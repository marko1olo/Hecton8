using UnityEngine;

namespace Hecton8.Input
{
    /// <summary>
    /// Central storage owner for user options backed by <see cref="PlayerPrefs"/>.
    /// Keeps option persistence out of UI shells and scene controllers.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-30995)]
    public sealed class UserOptionsPersistence : MonoBehaviour
    {
        /// <summary>
        /// Saved language key used by localization.
        /// Kept here so option storage has a single owner.
        /// </summary>
        public const string LanguageKey = "Hecton_Language";

        private static UserOptionsPersistence _instance;
        private static bool _isShuttingDown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            _isShuttingDown = false;
        }

        public static UserOptionsPersistence Instance
        {
            get
            {
                if (_isShuttingDown || !Application.isPlaying)
                    return _instance;

                if (_instance != null)
                    return _instance;

                GameObject go = new GameObject("[UserOptionsPersistence]");
                _instance = go.AddComponent<UserOptionsPersistence>();
                return _instance;
            }
        }

        public static UserOptionsPersistence EnsureRuntimeInstance()
        {
            return Instance;
        }

        public static bool TryGetInstance(out UserOptionsPersistence instance)
        {
            instance = _instance;
            return instance != null;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _isShuttingDown = false;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            _isShuttingDown = true;
        }

        public bool HasKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && PlayerPrefs.HasKey(key);
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            if (string.IsNullOrWhiteSpace(key))
                return defaultValue;

            return PlayerPrefs.GetInt(key, defaultValue);
        }

        public bool TryGetInt(string key, out int value)
        {
            if (HasKey(key))
            {
                value = PlayerPrefs.GetInt(key, 0);
                return true;
            }

            value = default;
            return false;
        }

        public void SetInt(string key, int value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            PlayerPrefs.SetInt(key, value);
        }

        public float GetFloat(string key, float defaultValue = 0f)
        {
            if (string.IsNullOrWhiteSpace(key))
                return defaultValue;

            return PlayerPrefs.GetFloat(key, defaultValue);
        }

        public bool TryGetFloat(string key, out float value)
        {
            if (HasKey(key))
            {
                value = PlayerPrefs.GetFloat(key, 0f);
                return true;
            }

            value = default;
            return false;
        }

        public void SetFloat(string key, float value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            PlayerPrefs.SetFloat(key, value);
        }

        public string GetString(string key, string defaultValue = "")
        {
            if (string.IsNullOrWhiteSpace(key))
                return defaultValue ?? string.Empty;

            return PlayerPrefs.GetString(key, defaultValue ?? string.Empty);
        }

        public bool TryGetString(string key, out string value)
        {
            if (HasKey(key))
            {
                value = PlayerPrefs.GetString(key, string.Empty);
                return true;
            }

            value = string.Empty;
            return false;
        }

        public void SetString(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            PlayerPrefs.SetString(key, value ?? string.Empty);
        }

        public bool GetBool(string key, bool defaultValue = false)
        {
            return GetInt(key, defaultValue ? 1 : 0) != 0;
        }

        public bool TryGetBool(string key, out bool value)
        {
            if (TryGetInt(key, out int stored))
            {
                value = stored != 0;
                return true;
            }

            value = default;
            return false;
        }

        public void SetBool(string key, bool value)
        {
            SetInt(key, value ? 1 : 0);
        }

        public void DeleteKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            if (PlayerPrefs.HasKey(key))
                PlayerPrefs.DeleteKey(key);
        }

        public void Save()
        {
            PlayerPrefs.Save();
        }
    }
}
