// ============================================================================
// HECTON-8 — ICuttable.cs
// Kontrakt dlya obektov, kotorye mozhno rezat lazerom.
//
// REALIZATsII:
//   • ResourceNode  — resursnyy uzel, delegiruet v TakeDamage.
//   • BaseModule    — modul bazy, delegiruet v ApplyDamage.
//
// POTREBITELI:
//   • LaserCutter.UsePrimary() — vyzyvaet ApplyCutDamage cherez
//     TryGetComponent<ICuttable> na reykast-tseli.
//
// KONTRAKT:
//   • damage — uron za kadr (damagePerSecond × deltaTime).
//     Garantiya vyzyvayuschey storony: damage > 0.
//   • hitPoint — mirovaya tochka popadaniya lucha (Vector3).
//     Realizatsiya mozhet ispolzovat dlya dekaley, VFX, napravlennyh
//     povrezhdeniy. Mozhet ignorirovat.
//
// ZERO GC:
//   • Interfeys bez svoystv — TryGetComponent<ICuttable> ne vyzyvaet
//     boxing (Unity keshiruet interfeysnye zaprosy na MonoBehaviour).
//   • Parametry — value types (float, Vector3).
// ============================================================================

using UnityEngine;

namespace Hecton8.Gameplay
{
    public interface ICuttable
    {
        /// <summary>
        /// Primenyaet uron ot rezhuschego instrumenta.
        /// </summary>
        /// <param name="damage">
        /// Uron za tekuschiy kadr. Polozhitelnoe znachenie.
        /// Tipichnyy istochnik: damagePerSecond × deltaTime.
        /// </param>
        /// <param name="hitPoint">
        /// Mirovaya pozitsiya tochki popadaniya lucha / instrumenta.
        /// Ispolzuetsya dlya lokalizatsii povrezhdeniy, spavna dekaley,
        /// napravlennyh VFX.
        /// </param>
        void ApplyCutDamage(float damage, Vector3 hitPoint);
    }
}