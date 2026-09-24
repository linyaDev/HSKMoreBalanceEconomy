using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    [StaticConstructorOnStartup]
    public static class AmmoCraftNerf
    {
        static AmmoCraftNerf()
        {
            var settings = HardcoreSettingsDef.Instance;
            float ammoMult = settings?.ammoCraftCostMultiplier ?? NerfSettings.ammoCraftCostMultiplier;
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

            int patchedAmmo = 0;
            int patchedArrow = 0;
            int patchedBolt = 0;
            foreach (var recipe in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                if (!recipe.defName.StartsWith("MakeAmmo_"))
                    continue;

                if (recipe.ingredients == null)
                    continue;

                if (excludedRecipes.Contains(recipe.defName))
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

                if (mult == 1f)
                    continue;

                foreach (var ing in recipe.ingredients)
                {
                    // Проверяем исключения по материалу
                    bool skip = false;
                    if (excluded.Count > 0 && ing.filter != null)
                    {
                        foreach (var allowedDef in ing.filter.AllowedThingDefs)
                        {
                            if (excluded.Contains(allowedDef.defName))
                            {
                                skip = true;
                                break;
                            }
                        }
                    }
                    if (skip)
                        continue;

                    float before = ing.GetBaseCount();
                    ing.SetBaseCount(before * mult);
                }
            }
            Log.Message($"[HSKMoreHardcore] AmmoCraftNerf: ammo x{ammoMult} ({patchedAmmo}), arrows x{arrowMult} ({patchedArrow}), bolts x{boltMult} ({patchedBolt}).");
        }
    }
}
