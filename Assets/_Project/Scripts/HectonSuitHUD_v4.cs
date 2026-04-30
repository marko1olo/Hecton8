using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Gameplay;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[ExecuteAlways]
[AddComponentMenu("Hecton8/HUD/Suit HUD v4")]
public sealed class HectonSuitHUD_v4 : MonoBehaviour, ITickable, IUpdatable
{
    // COLD ALLOC: List<HectonSuitHUD_v4>[4] — active legacy HUD compatibility registry — owner: HectonSuitHUD_v4
    private static readonly List<HectonSuitHUD_v4> s_activeHuds = new List<HectonSuitHUD_v4>(4);

    [Header("Legacy Compatibility")]
    [SerializeField] private Camera hudCamera;
    [SerializeField] private SuitHUDProfile fallbackProfile;
    public Camera HudCamera => hudCamera;

    public static void CopyActiveHudsTo(List<HectonSuitHUD_v4> results)
    {
        if (results == null)
            return;

        results.Clear();
        for (int i = 0; i < s_activeHuds.Count; i++)
        {
            HectonSuitHUD_v4 hud = s_activeHuds[i];
            if (hud != null && hud.isActiveAndEnabled)
                results.Add(hud);
        }
    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        if (EditorApplication.isCompiling || !Application.isPlaying)
            return;
#endif

        RegisterActiveHud();
        TryRegisterUiService();
    }

    private void OnDisable()
    {
        UnregisterUiService();
        UnregisterActiveHud();
    }

    public void Tick(float deltaTime)
    {
    }

    public void SetHudCamera(Camera camera)
    {
        hudCamera = camera;
    }

    public void SetFallbackProfile(SuitHUDProfile profile)
    {
        fallbackProfile = profile;
    }

    private void RegisterActiveHud()
    {
        for (int i = 0; i < s_activeHuds.Count; i++)
        {
            if (ReferenceEquals(s_activeHuds[i], this))
                return;
        }

        s_activeHuds.Add(this);
    }

    private void UnregisterActiveHud()
    {
        for (int i = s_activeHuds.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(s_activeHuds[i], this))
                s_activeHuds.RemoveAt(i);
        }
    }

    private void TryRegisterUiService()
    {
    }

    private void UnregisterUiService()
    {
    }
}
