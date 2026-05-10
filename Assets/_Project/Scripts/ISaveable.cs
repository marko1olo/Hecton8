// ============================================================================
// HECTON-8 — ISaveable.cs
// Interfeys dlya lyuboy sistemy, uchastvuyuschey v sohranenii/zagruzke.
//
// Realizuetsya MonoBehaviour-sistemami:
//   • HectonSurvivalSystem (staty igroka)
//   • PlayerInventory (soderzhimoe inventarya)
//   • WorldStateManager (sostoyanie resursnyh uzlov)
//   • ConstructionManager (postroennye moduli)
//
// PRIORITETY:
//   Chisla menshe = obrabatyvayutsya ranshe.
//   Pri sohranenii poryadok obychno ne kritichen.
//   Pri zagruzke poryadok VAZhEN:
//     10 — Player stats (pozitsiya, HP) → snachala
//     20 — Inventory → posle igroka
//     50 — World state → posle inventarya
//     90 — Construction → poslednim (mozhet zaviset ot mira)
// ============================================================================

namespace Hecton8.SaveSystem
{
    /// <summary>
    /// Kontrakt dlya sistem, uchastvuyuschih v save/load.
    /// </summary>
    public interface ISaveable
    {
        /// <summary>
        /// Prioritet pri sohranenii. Menshe = ranshe.
        /// Rekomendatsii: Player=10, Inventory=20, World=50, Construction=90.
        /// </summary>
        int SavePriority { get; }

        /// <summary>
        /// Prioritet pri zagruzke. Menshe = ranshe.
        /// KRITIChNO: igrok dolzhen zagruzhatsya pervym.
        /// </summary>
        int LoadPriority { get; }

        /// <summary>
        /// Zapisyvaet tekuschee sostoyanie sistemy v SaveData.
        /// Vyzyvaetsya SaveManager pri sohranenii.
        ///
        /// KONTRAKT:
        ///   • Zapolnyat TOLKO svoyu sektsiyu DTO.
        ///   • Ne trogat chuzhie sektsii.
        ///   • Ne allotsirovat novye massivy (ispolzovat EnsureCapacity).
        /// </summary>
        void PopulateSaveData(SaveData data);

        /// <summary>
        /// Vosstanavlivaet sostoyanie sistemy iz SaveData.
        /// Vyzyvaetsya SaveManager pri zagruzke.
        ///
        /// KONTRAKT:
        ///   • Chitat TOLKO svoyu sektsiyu DTO.
        ///   • Validirovat dannye pered primeneniem.
        ///   • Pri oshibkah — ispolzovat defoltnye znacheniya, ne krashit.
        /// </summary>
        void LoadFromSaveData(SaveData data);
    }
}