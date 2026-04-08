using Hecton8.Core;
using UnityEngine;

/// <summary>
/// Обновляет _GlobalRotation на материале планеты каждый кадр.
/// frac() на стороне C# гарантирует, что значение НИКОГДА
/// не выйдет за пределы [0,1] — precision loss невозможен.
/// </summary>
public sealed class GasGiantRotationDriver : MonoBehaviour, ITickable
{
    [SerializeField] private Renderer _planetRenderer;
    [SerializeField] private float _baseRotationSpeed = 1f;

    private MaterialPropertyBlock _mpb;
    private double _accumulatedRotation;
    private bool _registeredToTickManager;
    private static readonly int _idGlobalRotation =
        Shader.PropertyToID("_GlobalRotation");

    private void Awake()
    {
        EnsureRendererResources();
    }

    private void OnEnable()
    {
        EnsureRendererResources();

        if (_registeredToTickManager || GameTickManager.Instance == null)
            return;

        GameTickManager.Instance.Register((ITickable)this);
        _registeredToTickManager = true;
    }

    private void OnDisable()
    {
        if (!_registeredToTickManager || GameTickManager.Instance == null)
            return;

        GameTickManager.Instance.Unregister((ITickable)this);
        _registeredToTickManager = false;
    }

    public void Tick(float deltaTime)
    {
        if (_planetRenderer == null)
            return;

        _accumulatedRotation += (double)_baseRotationSpeed * deltaTime;

        float rotation = (float)(_accumulatedRotation % 1.0);

        _planetRenderer.GetPropertyBlock(_mpb);
        _mpb.SetFloat(_idGlobalRotation, rotation);
        _planetRenderer.SetPropertyBlock(_mpb);
    }

    private void EnsureRendererResources()
    {
        _mpb ??= new MaterialPropertyBlock();

        if (_planetRenderer == null)
            _planetRenderer = GetComponent<Renderer>();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            EnsureRendererResources();
    }
#endif
}
