using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    /// <summary>
    /// Множитель стоимости крафта боеприпасов: ингредиенты рецептов MakeAmmo_*
    /// умножаются на коэффициент из настроек. Базовые значения запоминаются при
    /// первом применении, поэтому множитель можно менять прямо в игре
    /// (Options -> Mod settings -> HSK More Balance) — Apply пересчитывает от
    /// базы, а не от уже умноженного.
    ///
    /// Множитель патронов берётся из настроек мода, если игрок его переопределил
    /// (ammoCraftOverride), иначе — из Defs/Misc/HardcoreSettings.xml.
    /// Стрелы и болты правятся только дефом.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class AmmoCraftNerf
    {
        // Исходные количества до любых наших правок: рецепт -> (ингредиент -> count)
        private static readonly Dictionary<RecipeDef, Dictionary<IngredientCount, float>> baseCounts
            = new Dictionary<RecipeDef, Dictionary<IngredientCount, float>>();
        // Партия меньше этой не множится: поштучные и малопартийные тяжёлые
        // снаряды (гаубица, пушки, РПГ, СПГ-9 — выход 1-8) с x3 стоили
        // под тысячу серебра за выстрел.
        private const int MinBatchSize = 20;

        static AmmoCraftNerf()
        {
            Apply();
        }

        /// <summary>
        /// Пересчитывает стоимость всех рецептов боеприпасов от исходных
        /// значений. Зовётся при старте и при смене множителя в настройках.
        /// </summary>
        public static void Apply()
        {
            var settings = HardcoreSettingsDef.Instance;
            float ammoMult = AmmoMultiplier;
            float arrowMult = settings?.arrowCraftCostMultiplier ?? 1f;
            float boltMult = settings?.boltCraftCostMultiplier ?? 2f;

            var excluded = new HashSet<string>();
            if (settings?.ammoCraftExcludedMaterials != null)
            {
                foreach (var mat in settings.ammoCraftExcludedMaterials)
                    excluded.Add(mat);
            }

            var excludedRecipes = new HashSet<string>();
            if (settings?.ammoCraftExcludedRecipes != null)
            {
                foreach (var r in settings.ammoCraftExcludedRecipes)
                    excludedRecipes.Add(r);
            }

            int patchedAmmo = 0, patchedArrow = 0, patchedBolt = 0;
            foreach (var recipe in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                if (!recipe.defName.StartsWith("MakeAmmo_") || recipe.ingredients == null)
                    continue;
                if (excludedRecipes.Contains(recipe.defName))
                    continue;
                if (BatchSize(recipe) < MinBatchSize)
                    continue;

                float mult;
                if (recipe.defName.StartsWith("MakeAmmo_Arrow_"))
                {
                    mult = arrowMult;
                    patchedArrow++;
                }
                else if (recipe.defName.StartsWith("MakeAmmo_Bolt_"))
                {
                    mult = boltMult;
                    patchedBolt++;
                }
                else
                {
                    mult = ammoMult;
                    patchedAmmo++;
                }

                foreach (var ing in recipe.ingredients)
                {
                    if (IsExcludedMaterial(ing, excluded))
                        continue;

                    float baseCount = BaseCountOf(recipe, ing);
                    ing.SetBaseCount(baseCount * mult);
                }
            }
            HMHLog.Message($"[HSKMoreHardcore] AmmoCraftNerf: ammo x{ammoMult} ({patchedAmmo}), arrows x{arrowMult} ({patchedArrow}), bolts x{boltMult} ({patchedBolt}).");
        }

        /// <summary>
        /// Действующий множитель патронов: переопределение из настроек мода,
        /// иначе значение из дефа.
        /// </summary>
        public static float AmmoMultiplier
        {
            get
            {
                var mod = HSKMoreHardcoreMod.Settings;
                if (mod != null && mod.ammoCraftOverride)
                    return mod.ammoCraftMultiplier;
                return HardcoreSettingsDef.Instance?.ammoCraftCostMultiplier
                    ?? NerfSettings.ammoCraftCostMultiplier;
            }
        }

        /// <summary>Значение множителя из дефа — показывается в настройках как «по умолчанию».</summary>
        public static float DefaultAmmoMultiplier =>
            HardcoreSettingsDef.Instance?.ammoCraftCostMultiplier ?? NerfSettings.ammoCraftCostMultiplier;

        private static int BatchSize(RecipeDef recipe)
        {
            int batch = 0;
            if (recipe.products != null)
            {
                foreach (var p in recipe.products)
                    batch += p.count;
            }
            return batch;
        }

        private static bool IsExcludedMaterial(IngredientCount ing, HashSet<string> excluded)
        {
            if (excluded.Count == 0 || ing.filter == null)
                return false;
            foreach (var allowedDef in ing.filter.AllowedThingDefs)
            {
                if (excluded.Contains(allowedDef.defName))
                    return true;
            }
            return false;
        }

        // Исходное количество ингредиента. При первом проходе запоминаем то,
        // что пришло из дефов; дальше всегда считаем от него.
        private static float BaseCountOf(RecipeDef recipe, IngredientCount ing)
        {
            if (!baseCounts.TryGetValue(recipe, out var perIngredient))
            {
                perIngredient = new Dictionary<IngredientCount, float>();
                baseCounts[recipe] = perIngredient;
            }

            if (!perIngredient.TryGetValue(ing, out float baseCount))
            {
                // На первом проходе в рецепте ещё исходные числа; если ингредиент
                // появился позже (чужой патч в рантайме), берём текущее значение.
                baseCount = ing.GetBaseCount();
                perIngredient[ing] = baseCount;
            }

            return baseCount;
        }
    }
}
