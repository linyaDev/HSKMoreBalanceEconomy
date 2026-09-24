using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    /// <summary>
    /// Уменьшает отрастание деревьев на карте (не затрагивает начальную генерацию).
    /// Postfix на CheckSpawnWildPlantAt: если дерево выросло во время игры —
    /// с шансом (1 - treeRegrowthChance) удаляем его.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class TreeRegrowthNerf
    {
        static TreeRegrowthNerf()
        {
            var harmony = new Harmony("linya.hskmorehardcore.treeregrowthnerf");
            var target = AccessTools.Method(typeof(WildPlantSpawner), nameof(WildPlantSpawner.CheckSpawnWildPlantAt));
            if (target != null)
            {
                harmony.Patch(target,
                    postfix: new HarmonyMethod(typeof(TreeRegrowthNerf), nameof(Postfix)));
                Log.Message("[HSKMoreHardcore] TreeRegrowthNerf applied.");
            }
        }

        public static void Postfix(bool __result, IntVec3 c, bool setRandomGrowth, Map ___map)
        {
            if (!__result || setRandomGrowth)
                return;

            var settings = HardcoreSettingsDef.Instance;
            float chance = settings?.treeRegrowthChance ?? 0.25f;
            if (chance >= 1f)
                return;

            var plant = c.GetPlant(___map);
            if (plant == null)
                return;

            bool isTree = plant.def.plant.IsTree;
            bool yieldsWood = plant.def.plant.harvestedThingDef != null
                && (plant.def.plant.harvestedThingDef.defName == "WoodLog"
                    || plant.def.plant.harvestedThingDef.defName == "Kindling");
            // Фруктовые деревья HSK для игры деревьями не являются: harvestTag=Standard,
            // forceIsTree не выставлен, урожай — плоды. Опознаём их по пню после рубки.
            bool leavesStump = plant.def.plant.choppedThingDef != null;

            if (!isTree && !yieldsWood && !leavesStump)
                return;

            if (!Rand.Chance(chance))
            {
                plant.Destroy();
            }
        }
    }
}
