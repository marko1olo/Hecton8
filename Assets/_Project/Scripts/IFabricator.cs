// ============================================================================
// HECTON-8 — IFabricator.cs
// Kontrakt dlya verstakov (fabrikatorov), s kotorymi vzaimodeystvuet UI.
//
// REALIZATsII:
//   • Fabricator — mashina-verstak s IPowerComponent, IInteractable, ITickable.
//
// POTREBITELI:
//   • HectonFabricatorUI — subscribes through ICraftingEventListener for FabricatorOpened payloads,
//     chitaet AvailableRecipes, IsCrafting, vyzyvaet StartCraft/CancelCraft.
//   • CraftingEvents — peredaet IFabricator v sobytii otkrytiya.
//
// KONTRAKT:
//   • AvailableRecipes — spisok retseptov, dostupnyh na etom verstake.
//     IReadOnlyList garantiruet, chto UI ne mozhet mutirovat kollektsiyu.
//     Realizatsiya vozvraschaet List<RecipeData> cherez implicit cast.
//
//   • IsCrafting — true, poka idet protsess krafta (taymer tikaet
//     ili zamorozhen iz-za otsutstviya pitaniya).
//
//   • StartCraft(recipe) — zapuskaet kraft. Realizatsiya:
//     proveryaet CanCraft, spisyvaet ingredienty, zapuskaet taymer.
//     UI vyzyvaet tolko esli IsCrafting == false.
//
//   • CancelCraft() — otmenyaet tekuschiy kraft, vozvraschaet ingredienty.
//     Bezopasen pri vyzove bez aktivnogo krafta (no-op).
//
// ZERO GC:
//   • IReadOnlyList<T> — interfeys nad List<T>, zero allocation
//     (implicit cast, bez sozdaniya obertki).
//   • Svoystva vozvraschayut value types (bool) i ssylochnyy tip
//     (IReadOnlyList) bez boxing.
// ============================================================================

using System.Collections.Generic;

namespace Hecton8.Crafting
{
    public interface IFabricator
    {
        /// <summary>
        /// Spisok retseptov, dostupnyh na etom verstake.
        /// UI ispolzuet dlya otobrazheniya menyu krafta.
        /// Ne mozhet byt null — realizatsiya garantiruet pustoy spisok.
        /// </summary>
        IReadOnlyList<RecipeData> AvailableRecipes { get; }

        /// <summary>
        /// Idet li protsess krafta.
        /// true — taymer aktiven (ili zamorozhen pri otsutstvii pitaniya).
        /// false — verstak svoboden dlya novogo krafta.
        /// </summary>
        bool IsCrafting { get; }

        /// <summary>
        /// Zapuskaet protsess krafta ukazannogo retsepta.
        /// Realizatsiya proveryaet nalichie ingredientov, pitaniya,
        /// spisyvaet resursy i zapuskaet taymer.
        /// </summary>
        /// <param name="recipe">
        /// Retsept dlya krafta. Ne null.
        /// Dolzhen prisutstvovat v AvailableRecipes.
        /// </param>
        void StartCraft(RecipeData recipe);

        void StartCraft(RecipeData recipe, int multiplier);

        /// <summary>
        /// Otmenyaet tekuschiy kraft.
        /// Vozvraschaet spisannye ingredienty v inventar igroka.
        /// Bezopasen pri vyzove bez aktivnogo krafta (no-op).
        /// </summary>
        void CancelCraft();
    }
}
