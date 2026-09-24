using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    // Снижает ШАНС хорошего качества при крафте игроком в зависимости от материала.
    // Множитель берётся из стата материала HSK_CraftQualityFactor (виден в карточке кожи):
    // factor = шанс удержать каждый тир качества. После того как качество выпало, пока q > Awful
    // и бросок не прошёл (Rand.Value >= factor), опускаем на тир. factor=1 — без изменений.
    //
    // Патчим Verse.GenRecipe.PostProcessProduct — это путь крафта по рецепту, поэтому торговцев,
    // награды и прочую генерацию не затрагивает (только сделанное игроком).
    [StaticConstructorOnStartup]
    public static class StuffCraftQuality
    {
        private static StatDef factorStat;
        private static readonly FieldInfo qualityIntField = AccessTools.Field(typeof(CompQuality), "qualityInt");

        static StuffCraftQuality()
        {
            var method = AccessTools.Method("Verse.GenRecipe:PostProcessProduct");
            if (method == null)
                return;

            var harmony = new Harmony("linya.hskmorehardcore.stuffcraftquality");
            harmony.Patch(method, postfix: new HarmonyMethod(typeof(StuffCraftQuality), nameof(Postfix)));
            Log.Message("[HSKMoreHardcore] StuffCraftQuality applied.");
        }

        public static void Postfix(Thing __result, Pawn worker)
        {
            var product = __result;
            if (product == null || qualityIntField == null)
                return;
            if (worker?.Faction == null || !worker.Faction.IsPlayer) // только крафт игроком
                return;
            if (product.Stuff == null)
                return;

            var cq = product.TryGetComp<CompQuality>();
            if (cq == null)
                return;

            if (factorStat == null)
                factorStat = DefDatabase<StatDef>.GetNamedSilentFail("HSK_CraftQualityFactor");
            if (factorStat == null)
                return;

            float factor = product.Stuff.GetStatValueAbstract(factorStat);
            if (factor >= 1f)
                return;

            int before = (int)cq.Quality;
            int q = before;
            while (q > 0 && Rand.Value >= factor)
                q--;

            if (q != before)
                qualityIntField.SetValue(cq, (QualityCategory)q);
        }
    }
}
