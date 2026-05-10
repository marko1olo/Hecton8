// ============================================================================
// HECTON-8 — IPowerComponent.cs
// Kontrakt dlya lyubogo komponenta energosistemy bazy.
//
// Realizuetsya komponentami na modulyah bazy:
//   • PowerNode        — bazovoe potreblenie modulya (iz BuildableData)
//   • Fabricator       — dopolnitelnoe potreblenie pri krafte
//   • LifeSupport      — potreblenie sistemy zhizneobespecheniya (buduschee)
//   • SolarPanel       — generatsiya (polozhitelnyy PowerRating)
//   • ThermalReactor   — generatsiya (buduschee)
//
// PowerGrid sobiraet vse IPowerComponent iz vseh PowerNode v seti.
// UpdateBalance() summiruet PowerRating i upravlyaet otklyucheniem.
//
// SOGLAShENIYa:
//   PowerRating > 0   → generator (vyrabatyvaet energiyu, Vt).
//   PowerRating < 0   → potrebitel (potreblyaet energiyu, Vt).
//   PowerRating == 0  → passivnyy (koridory, steny bez elektroniki).
//
//   PowerPriority:
//     0   = kriticheskiy (zhizneobespechenie) — otklyuchaetsya POSLEDNIM.
//     50  = obychnyy (fabrikatory, osveschenie).
//     100 = roskosh (dekor, akvariumy) — otklyuchaetsya PERVYM.
//
//   Generatory (PowerRating > 0) NIKOGDA ne otklyuchayutsya.
//   OnPowerStatusChanged vyzyvaetsya TOLKO dlya potrebiteley.
//
// ZERO GC:
//   Vse svoystva vozvraschayut value types (float, int, bool).
//   OnPowerStatusChanged — vyzov metoda, no boxing.
// ============================================================================

namespace Hecton8.Power
{
    /// <summary>
    /// Interfeys energokomponenta bazy.
    /// Lyuboy komponent na module bazy mozhet realizovat etot
    /// interfeys dlya uchastiya v energeticheskom balanse seti.
    /// </summary>
    public interface IPowerComponent
    {
        /// <summary>
        /// Energeticheskiy reyting komponenta (Vatty).
        ///
        /// Polozhitelnyy = generatsiya (solnechnaya panel: +200).
        /// Otritsatelnyy = potreblenie (fabrikator pri krafte: -100).
        /// Nol = passivnyy (ne vliyaet na balans).
        ///
        /// Mozhet menyatsya dinamicheski:
        ///   • Fabricator: 0 v idle, -100 pri krafte.
        ///   • SolarPanel: +200 dnem, +50 nochyu.
        ///
        /// Vyzyvaetsya PowerGrid.UpdateBalance() kazhdyy SlowTick.
        /// </summary>
        float PowerRating { get; }

        /// <summary>
        /// Prioritet otklyucheniya pri defitsite energii.
        ///
        /// 0   = kriticheskiy (zhizneobespechenie) — otklyuchit POSLEDNIM.
        /// 50  = obychnyy (standartnye moduli).
        /// 100 = roskosh (dekorativnye) — otklyuchit PERVYM.
        ///
        /// Pri defitsite potrebiteli sortiruyutsya po prioritetu DESC:
        /// vysokiy prioritet (100) otklyuchaetsya pervym.
        /// Generatory (PowerRating > 0) ignoriruyut etot parametr.
        /// </summary>
        int PowerPriority { get; }

        /// <summary>
        /// Tekuschee sostoyanie pitaniya (keshirovannoe).
        ///
        /// true = energiya podaetsya, komponent rabotaet.
        /// false = energiya otklyuchena, komponent v rezhime ozhidaniya.
        ///
        /// Ustanavlivaetsya cherez OnPowerStatusChanged.
        /// Nachalnoe znachenie: true (do pervogo UpdateBalance).
        /// </summary>
        bool HasPower { get; }

        /// <summary>
        /// Uvedomlenie ob izmenenii statusa pitaniya.
        ///
        /// Vyzyvaetsya PowerGrid.UpdateBalance() pri IZMENENII sostoyaniya:
        ///   • true → false: energiya poteryana, komponent dolzhen priostanovit rabotu.
        ///   • false → true: energiya vosstanovlena, komponent mozhet vozobnovit.
        ///
        /// NE vyzyvaetsya kazhdyy tik — tolko pri perehode.
        ///
        /// Realizatsiya dolzhna:
        ///   1. Keshirovat znachenie hasPower.
        ///   2. Priostanovit/vozobnovit svoyu logiku.
        ///   3. Obnovit vizual (vyklyuchit svet, ostanovit animatsiyu).
        /// </summary>
        /// <param name="hasPower">true = pitanie est, false = net.</param>
        void OnPowerStatusChanged(bool hasPower);
    }
}