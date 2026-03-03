// ============================================================================
//  BuoyancyObject.cs — Компонент плавучести.
//  Вешается на любой GameObject с Rigidbody.
//  Автоматически регистрируется в HectonFluidEngine для batch-обработки.
// ============================================================================
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
[AddComponentMenu("Hecton/Buoyancy Object")]
public class BuoyancyObject : MonoBehaviour
{
    // ======================== INSPECTOR ========================

    [Header("═══════ Плавучесть ═══════")]

    [Tooltip(
        "Множитель выталкивающей силы Архимеда:\n" +
        "  > 1.0 — всплывает  (дерево ~ 2.0)\n" +
        "  = 1.0 — нейтрально\n" +
        "  < 1.0 — тонет      (металл ~ 0.13)")]
    [Range(0.01f, 5f)]
    public float buoyancyMultiplier = 1.5f;

    [Tooltip("Приблизительная высота объекта (м).\n" +
             "Определяет расчёт частичного погружения.")]
    [Min(0.01f)]
    public float objectHeight = 1f;

    [Header("═══════ Сопротивление среды ═══════")]

    [Tooltip("Линейное затухание скорости под водой")]
    [Range(0f, 25f)]
    public float underwaterDrag = 3f;

    [Tooltip("Угловое затухание вращения под водой")]
    [Range(0f, 25f)]
    public float underwaterAngularDrag = 1.5f;

    // ======================== RUNTIME ========================

    /// <summary>0 = над водой, 1 = полностью под водой.</summary>
    [System.NonSerialized] public float  SubmersionRatio;

    /// <summary>Вектор течения в позиции объекта (м/с).</summary>
    [System.NonSerialized] public float3 CurrentVector;

    /// <summary>Кешированная ссылка на Rigidbody.</summary>
    public Rigidbody Rb { get; private set; }

    public bool IsSubmerged      => SubmersionRatio > 0f;
    public bool IsFullySubmerged => SubmersionRatio >= 0.999f;

    // ============= ОТЛОЖЕННАЯ РЕГИСТРАЦИЯ =============
    private static readonly List<BuoyancyObject> _pending =
        new List<BuoyancyObject>(64);

    internal static void FlushPending(HectonFluidEngine engine)
    {
        for (int i = 0; i < _pending.Count; i++)
        {
            var obj = _pending[i];
            if (obj != null && obj.isActiveAndEnabled)
                engine.Register(obj);
        }
        _pending.Clear();
    }

    // ======================== LIFECYCLE ========================

    void Awake()
    {
        Rb = GetComponent<Rigidbody>();
    }

    void OnEnable()
    {
        if (HectonFluidEngine.Instance != null)
            HectonFluidEngine.Instance.Register(this);
        else
            _pending.Add(this);
    }

    void OnDisable()
    {
        if (HectonFluidEngine.Instance != null)
            HectonFluidEngine.Instance.Unregister(this);

        _pending.Remove(this);
        SubmersionRatio = 0f;
        CurrentVector   = float3.zero;
    }

    // ======================== GIZMOS ========================

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = IsSubmerged
            ? new Color(0f, 0.7f, 1f, 0.35f)
            : new Color(1f, 1f, 0f, 0.25f);
        Gizmos.DrawWireCube(transform.position,
                            new Vector3(0.4f, objectHeight, 0.4f));

        if (HectonFluidEngine.Instance != null)
        {
            float wl = HectonFluidEngine.Instance.waterLevel;
            Vector3 mark = new Vector3(
                transform.position.x,
                wl,
                transform.position.z);

            Gizmos.color = new Color(0f, 0.4f, 1f, 0.8f);
            Gizmos.DrawLine(mark + Vector3.left,    mark + Vector3.right);
            Gizmos.DrawLine(mark + Vector3.back,    mark + Vector3.forward);
        }
    }
#endif
}