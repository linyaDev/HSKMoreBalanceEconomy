using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKMoreHardcore
{
    [StaticConstructorOnStartup]
    public static class RecycleNerf
    {
        static RecycleNerf()
        {
            var reclaimMethod = AccessTools.Method("Mending.JobDriverUtils:Reclaim");
            if (reclaimMethod == null)
                return;

            var harmony = new Harmony("linya.hskmorehardcore.recyclenerf");
            harmony.Patch(reclaimMethod,
                postfix: new HarmonyMethod(typeof(RecycleNerf), nameof(Postfix)));
            Log.Message("[HSKMoreHardcore] RecycleNerf applied.");
        }

        public static void Postfix(ref List<Thing> __result, Thing thing, float efficiency)
        {
            if (!thing.def.IsApparel)
                return;

            // Металл: урезаем каждый ресурс до 10-15% от оригинала
            if (IsMetallic(thing))
            {
                float mult = Rand.Range(0.10f, 0.15f);
                for (int i = __result.Count - 1; i >= 0; i--)
                {
                    __result[i].stackCount = Mathf.Max(1, Mathf.FloorToInt(__result[i].stackCount * mult));
                }
                return;
            }

            var costList = CostListCalculator.CostListAdjusted(thing);
            int totalCost = 0;
            foreach (var cost in costList)
            {
                totalCost += cost.count;
            }

            if (totalCost <= 0)
                return;

            int amount = Mathf.Max(1, Mathf.FloorToInt(totalCost * Rand.Range(0.10f, 0.15f)));

            // Кожа даёт лоскутную кожу, всё остальное — лоскутную ткань: разбор возвращает
            // обрезки, а не полноценное сырьё.
            ThingDef outputDef = IsLeathery(thing) ? RecycleNerfDefs.Leather_Patch : RecycleNerfDefs.Cloth_Patch;

            Thing output = ThingMaker.MakeThing(outputDef);
            output.stackCount = amount;

            __result.Clear();
            __result.Add(output);
        }

        private static bool IsMetallic(Thing thing)
        {
            if (thing.Stuff?.stuffProps?.categories == null)
                return false;
            foreach (var cat in thing.Stuff.stuffProps.categories)
            {
                if (cat.defName.Contains("Metallic"))
                    return true;
            }
            return false;
        }

        private static bool IsLeathery(Thing thing)
        {
            if (thing.Stuff?.stuffProps?.categories == null)
                return false;
            foreach (var cat in thing.Stuff.stuffProps.categories)
            {
                if (cat.defName == "Leathery")
                    return true;
            }
            return false;
        }
    }

    [DefOf]
    public static class RecycleNerfDefs
    {
        public static ThingDef Leather_Patch;
        public static ThingDef Cloth_Patch;
    }
}
