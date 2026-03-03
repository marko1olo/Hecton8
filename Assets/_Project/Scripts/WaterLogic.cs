using UnityEngine;

/// 
/// WaterLogic — управляет параметрами тумана при погружении игрока под воду.
/// Вешается на объект Player. Проверяет transform.position.y каждый кадр.
/// 
public class WaterLogic : MonoBehaviour
{
    [Header("Настройки тумана над водой")]
    [Tooltip("Цвет тумана, когда игрок находится над поверхностью воды")]
    public Color aboveWaterFogColor = new Color(0.75f, 0.85f, 0.95f, 1f); // светло-голубой

    [Tooltip("Плотность тумана над водой")]
    public float aboveWaterFogDensity = 0.001f;

    [Header("Настройки тумана под водой")]
    [Tooltip("Цвет тумана, когда игрок находится под водой")]
    public Color underwaterFogColor = new Color(0.0f, 0.15f, 0.3f, 1f); // тёмно-синий/бирюзовый

    [Tooltip("Плотность тумана под водой")]
    public float underwaterFogDensity = 0.05f;

    [Header("Параметры перехода")]
    [Tooltip("Скорость плавного перехода между состояниями (чем больше — тем быстрее)")]
    [Range(0.5f, 15f)]
    public float transitionSpeed = 5f;

    [Tooltip("Уровень воды по оси Y (ниже этого значения — под водой)")]
    public float waterLevel = 0f;

    // Целевые значения, к которым стремимся в текущем кадре
    private Color _targetFogColor;
    private float _targetFogDensity;

    private void Start()
    {
        // Убеждаемся, что туман включён
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;

        // Инициализируем целевые значения на основе текущей позиции
        if (transform.position.y < waterLevel)
        {
            _targetFogColor = underwaterFogColor;
            _targetFogDensity = underwaterFogDensity;
        }
        else
        {
            _targetFogColor = aboveWaterFogColor;
            _targetFogDensity = aboveWaterFogDensity;
        }

        // Устанавливаем начальные значения сразу (без плавности при старте)
        RenderSettings.fogColor = _targetFogColor;
        RenderSettings.fogDensity = _targetFogDensity;
    }

    private void Update()
    {
        // Определяем, под водой ли игрок
        bool isUnderwater = transform.position.y < waterLevel;

        // Устанавливаем целевые значения
        _targetFogColor = isUnderwater ? underwaterFogColor : aboveWaterFogColor;
        _targetFogDensity = isUnderwater ? underwaterFogDensity : aboveWaterFogDensity;

        // Коэффициент интерполяции (зависит от времени для плавности)
        float lerpFactor = transitionSpeed * Time.deltaTime;

        // Плавный переход цвета тумана
        RenderSettings.fogColor = Color.Lerp(
            RenderSettings.fogColor,
            _targetFogColor,
            lerpFactor
        );

        // Плавный переход плотности тумана
        RenderSettings.fogDensity = Mathf.Lerp(
            RenderSettings.fogDensity,
            _targetFogDensity,
            lerpFactor
        );
    }
}