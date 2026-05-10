// ============================================================================
// HECTON-8 — IPoolable.cs
// Kontrakt dlya lyubogo obekta, prohodyaschego cherez Object Pool.
//
// Realizuetsya MonoBehaviour-komponentami na puliruemyh prefabah.
// Menedzher vyzyvaet eti metody avtomaticheski cherez TryGetComponent.
//
// VAZhNO: Realizatsiya OnDespawn() OBYaZANA sbrasyvat VSE vnutrennee
// sostoyanie obekta k «novorozhdennomu». Esli etogo ne sdelat,
// pri sleduyuschem Spawn obekt mozhet nesti «pamyat» predyduschey zhizni.
// ============================================================================

namespace Hecton8.Core
{
    /// <summary>
    /// Interfeys puliruemogo obekta.
    /// Lyuboy komponent na puliruemom prefabe mozhet realizovat etot
    /// interfeys dlya polucheniya uvedomleniy o zhiznennom tsikle pula.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// Vyzyvaetsya pri izvlechenii obekta iz pula (aktivatsiya).
        /// Analog Awake/Start, no dlya pereispolzuemogo obekta.
        ///
        /// Ispolzuy dlya:
        ///   • Zapuska VFX / AudioSource
        ///   • Initsializatsii nachalnyh znacheniy (HP, taymery)
        ///   • Vklyucheniya komponentov (Collider, Renderer)
        ///   • Zapuska korutin
        ///
        /// Vyzyvaetsya POSLE SetActive(true) i ustanovki pozitsii/povorota.
        /// </summary>
        void OnSpawn();

        /// <summary>
        /// Vyzyvaetsya pri vozvrate obekta v pul (deaktivatsiya).
        /// Analog OnDestroy, no obekt NE unichtozhaetsya — sbrasyvaetsya.
        ///
        /// Ispolzuy dlya:
        ///   • Obnuleniya Rigidbody.velocity / angularVelocity
        ///   • Ostanovki korutin (StopAllCoroutines)
        ///   • Ostanovki ParticleSystem / AudioSource
        ///   • Sbrosa HP, taymerov, flagov
        ///   • Otpiski ot sobytiy (esli podpisyvalsya v OnSpawn)
        ///   • Sbrosa parent (esli menyalsya pri zhizni)
        ///
        /// Vyzyvaetsya PERED SetActive(false).
        /// </summary>
        void OnDespawn();
    }
}