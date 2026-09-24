using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    // Продление жизни деревьев: ваниль убивает растение от старости на
    // growDays * lifespanDaysPerGrowDays (по умолчанию 8 — дуб живёт ~4.7 лет).
    // С нашим нерфом отрастания (treeRegrowthChance 0.03) лес вымирал бы сам.
    // На старте выставляем всем деревьям множитель из настроек
    // (treeLifespanDaysPerGrowDays, 0 = не трогать). Критерии дерева — те же,
    // что в TreeRegrowthNerf: IsTree / даёт дерево-растопку / оставляет пень.
    [StaticConstructorOnStartup]
    public static class TreeLifespanExtender
    {
        static TreeLifespanExtender()
        {
            float value = HardcoreSettingsDef.Instance?.treeLifespanDaysPerGrowDays ?? 0f;
            if (value <= 0f)
                return;

            int count = 0;
            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                var plant = def.plant;
                if (plant == null)
                    continue;

                bool isTree = plant.IsTree;
                bool yieldsWood = plant.harvestedThingDef != null
                    && (plant.harvestedThingDef.defName == "WoodLog"
                        || plant.harvestedThingDef.defName == "Kindling");
                bool leavesStump = plant.choppedThingDef != null;
                if (!isTree && !yieldsWood && !leavesStump)
                    continue;

                if (plant.lifespanDaysPerGrowDays > 0f && plant.lifespanDaysPerGrowDays < value)
                {
                    plant.lifespanDaysPerGrowDays = value;
                    count++;
                }
            }

            Log.Message($"[HSKMoreHardcore] TreeLifespanExtender: срок жизни x{value} growDays у деревьев — {count} дефов.");
        }
    }
}
