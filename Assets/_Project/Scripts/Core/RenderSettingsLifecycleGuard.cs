using System;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Core
{
    /// <summary>
    /// Captures the pre-runtime global render-settings block and restores it when the final visual owner releases.
    /// </summary>
    internal static class RenderSettingsLifecycleGuard
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct RenderSettingsSnapshot
        {
            public bool Fog;
            public FogMode FogMode;
            public Color FogColor;
            public float FogDensity;
            public AmbientMode AmbientMode;
            public Color AmbientLight;
            public Color AmbientSkyColor;
            public Color AmbientEquatorColor;
            public Color AmbientGroundColor;
            public float AmbientIntensity;
            public float ReflectionIntensity;
            public Material Skybox;
            public Light Sun;

            public static RenderSettingsSnapshot Capture()
            {
                return new RenderSettingsSnapshot
                {
                    Fog = RenderSettings.fog,
                    FogMode = RenderSettings.fogMode,
                    FogColor = RenderSettings.fogColor,
                    FogDensity = RenderSettings.fogDensity,
                    AmbientMode = RenderSettings.ambientMode,
                    AmbientLight = RenderSettings.ambientLight,
                    AmbientSkyColor = RenderSettings.ambientSkyColor,
                    AmbientEquatorColor = RenderSettings.ambientEquatorColor,
                    AmbientGroundColor = RenderSettings.ambientGroundColor,
                    AmbientIntensity = RenderSettings.ambientIntensity,
                    ReflectionIntensity = RenderSettings.reflectionIntensity,
                    Skybox = CaptureSkybox(),
                    Sun = RenderSettings.sun
                };
            }

            public void Restore()
            {
                RenderSettings.fog = Fog;
                RenderSettings.fogMode = FogMode;
                RenderSettings.fogColor = FogColor;
                RenderSettings.fogDensity = FogDensity;
                IGIRelaySystem giRelay = GlobalRegistry.GIRelay;
                bool giRelayAmbientAuthority = giRelay != null && giRelay.IsAmbientProbeAuthorityActive;
                if (!giRelayAmbientAuthority)
                {
                    RenderSettings.ambientMode = AmbientMode;
                    RenderSettings.ambientLight = AmbientLight;
                    RenderSettings.ambientSkyColor = AmbientSkyColor;
                    RenderSettings.ambientEquatorColor = AmbientEquatorColor;
                    RenderSettings.ambientGroundColor = AmbientGroundColor;
                    RenderSettings.ambientIntensity = AmbientIntensity;
                }
                RenderSettings.reflectionIntensity = ReflectionIntensity;
                RestoreSkybox(Skybox);
                RenderSettings.sun = Sun;
                if (!giRelayAmbientAuthority)
                    DynamicGI.UpdateEnvironment();
            }
        }

        // COLD ALLOC: ulong[4] - active render-settings lifecycle guard owners - owner: RenderSettingsLifecycleGuard
        private static ulong[] _ownerIds = new ulong[4];
        private static int _ownerCount;
        private static int _ownerOverflowCount;
        private static bool _snapshotCaptured;
        private static RenderSettingsSnapshot _snapshot;
#if UNITY_EDITOR
        private static bool _editorHooksRegistered;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _ownerCount = 0;
            _ownerOverflowCount = 0;
            _snapshotCaptured = false;
            _snapshot = default;
            Array.Clear(_ownerIds, 0, _ownerIds.Length);
        }

        public static void Acquire(UnityEngine.Object owner)
        {
            if (owner == null)
                return;

            ulong ownerId = EntityId.ToULong(owner.GetEntityId());
            if (IndexOfOwner(ownerId) >= 0)
                return;

            if (_ownerCount >= _ownerIds.Length)
            {
                _ownerOverflowCount++;
                return;
            }

            if (_ownerCount == 0)
            {
                _snapshot = RenderSettingsSnapshot.Capture();
                _snapshotCaptured = true;
#if UNITY_EDITOR
                RegisterEditorLifecycleHooks();
#endif
            }

            _ownerIds[_ownerCount] = ownerId;
            _ownerCount++;
        }

        public static void Release(UnityEngine.Object owner)
        {
            if (owner == null)
                return;

            ulong ownerId = EntityId.ToULong(owner.GetEntityId());
            int ownerIndex = IndexOfOwner(ownerId);
            if (ownerIndex < 0)
            {
                if (_ownerOverflowCount > 0)
                {
                    _ownerOverflowCount--;
                    RestoreIfNoOwnersRemain();
                }

                return;
            }

            _ownerCount--;
            if (ownerIndex < _ownerCount)
                _ownerIds[ownerIndex] = _ownerIds[_ownerCount];

            _ownerIds[_ownerCount] = 0;
            RestoreIfNoOwnersRemain();
        }

        private static void RestoreIfNoOwnersRemain()
        {
            if (_ownerCount != 0 || _ownerOverflowCount != 0 || !_snapshotCaptured)
                return;

            _snapshot.Restore();
            _snapshotCaptured = false;
            _snapshot = default;
            Array.Clear(_ownerIds, 0, _ownerIds.Length);
#if UNITY_EDITOR
            UnregisterEditorLifecycleHooks();
#endif
        }

        public static void ForceRestore()
        {
            if (_snapshotCaptured)
                _snapshot.Restore();

            _ownerCount = 0;
            _ownerOverflowCount = 0;
            _snapshotCaptured = false;
            _snapshot = default;
            Array.Clear(_ownerIds, 0, _ownerIds.Length);
#if UNITY_EDITOR
            UnregisterEditorLifecycleHooks();
#endif
        }

        private static int IndexOfOwner(ulong ownerId)
        {
            for (int i = 0; i < _ownerCount; i++)
            {
                if (_ownerIds[i] == ownerId)
                    return i;
            }

            return -1;
        }

        private static Material CaptureSkybox()
        {
            IAtmosphereRenderSettingsBridge bridge = GlobalRegistry.Atmosphere;
            return bridge != null
                ? bridge.Skybox
                : RenderSettings.skybox;
        }

        private static void RestoreSkybox(Material skybox)
        {
            IAtmosphereRenderSettingsBridge bridge = GlobalRegistry.Atmosphere;
            if (bridge != null)
            {
                bridge.SetSkybox(skybox);
                return;
            }

            if (!ReferenceEquals(RenderSettings.skybox, skybox))
                RenderSettings.skybox = skybox;
        }

#if UNITY_EDITOR
        private static void RegisterEditorLifecycleHooks()
        {
            if (Application.isBatchMode)
                return;

            if (_editorHooksRegistered)
                return;

            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
            EditorApplication.quitting -= HandleEditorQuitting;
            EditorApplication.quitting += HandleEditorQuitting;
            _editorHooksRegistered = true;
        }

        private static void UnregisterEditorLifecycleHooks()
        {
            if (!_editorHooksRegistered)
                return;

            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
            EditorApplication.quitting -= HandleEditorQuitting;
            _editorHooksRegistered = false;
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (Application.isBatchMode)
                return;

            if (state == PlayModeStateChange.ExitingEditMode ||
                state == PlayModeStateChange.ExitingPlayMode ||
                state == PlayModeStateChange.EnteredEditMode)
            {
                ForceRestore();
            }
        }

        private static void HandleBeforeAssemblyReload()
        {
            if (Application.isBatchMode)
                return;

            ForceRestore();
        }

        private static void HandleEditorQuitting()
        {
            if (Application.isBatchMode)
                return;

            ForceRestore();
        }
#endif
    }
}
