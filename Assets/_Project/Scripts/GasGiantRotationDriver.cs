using UnityEngine;

/// <summary>
/// Обновляет _GlobalRotation на материале планеты каждый кадр.
/// frac() на стороне C# гарантирует, что значение НИКОГДА
/// не выйдет за пределы [0,1] — precision loss невозможен.
/// </summary>
public class GasGiantRotationDriver : MonoBehaviour
{
    [SerializeField] private Renderer _planetRenderer;
    [SerializeField] private float _baseRotationSpeed = 1f;

    private MaterialPropertyBlock _mpb;
    private double _accumulatedRotation;
    private static readonly int _idGlobalRotation =
        Shader.PropertyToID("_GlobalRotation");

    void Awake()
    {
        _mpb = new MaterialPropertyBlock();

        if (_planetRenderer == null)
            _planetRenderer = GetComponent<Renderer>();
    }

    void Update()
    {
        if (_planetRenderer == null)
            return;

        if (_mpb == null)
            _mpb = new MaterialPropertyBlock();

        _accumulatedRotation += (double)_baseRotationSpeed * Time.deltaTime;

        float rotation = (float)(_accumulatedRotation % 1.0);

        _planetRenderer.GetPropertyBlock(_mpb);
        _mpb.SetFloat(_idGlobalRotation, rotation);
        _planetRenderer.SetPropertyBlock(_mpb);
    }
}
