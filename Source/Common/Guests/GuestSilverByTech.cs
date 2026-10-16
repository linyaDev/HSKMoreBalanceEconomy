using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKMoreHardcore
{
    // Серебро гостей Hospitality по техуровню их фракции: множитель из словаря
    // guestSilverMultiplierByTech (TechLevel -> множитель) в HardcoreSettings.xml.
    // Hospitality сам выдаёт серебро в IncidentWorker_VisitorGroup.GiveItems (от
    // отношений, титула и возраста, фракция не учитывается). Множим только это
    // серебро: префикс запоминает, сколько было у каждого гостя, постфикс досыпает
    // (множитель - 1) от выданного. Серебро из снаряжения вида пешки не трогаем.
    // Место в инвентаре проверяем тем же GetInventorySpaceFor, что и Hospitality
    // (с Combat Extended — по его инвентарю). Бюджет предметов на продажу не меняется.
    [StaticConstructorOnStartup]
    public static class GuestSilverByTech
    {
        private static readonly Func<Pawn, Thing, int> getInventorySpaceFor;

        // Серебро у гостей до GiveItems, заполняется префиксом
        private static readonly Dictionary<Pawn, int> silverBefore = new Dictionary<Pawn, int>();

        static GuestSilverByTech()
        {
            var worker = AccessTools.TypeByName("Hospitality.IncidentWorker_VisitorGroup");
            var utility = AccessTools.TypeByName("Hospitality.Utilities.ItemUtility");
            if (worker == null || utility == null)
                return; // Hospitality не установлен

            var giveItems = AccessTools.Method(worker, "GiveItems");
            var spaceFor = AccessTools.Method(utility, "GetInventorySpaceFor");
            if (giveItems == null || spaceFor == null)
            {
                Log.Warning("[HSKMoreHardcore] GuestSilverByTech: API Hospitality изменилось — серебро гостей не меняется.");
                return;
            }

            getInventorySpaceFor = AccessTools.MethodDelegate<Func<Pawn, Thing, int>>(spaceFor);

            new Harmony("linya.hskmorehardcore.guestsilverbytech").Patch(giveItems,
                prefix: new HarmonyMethod(typeof(GuestSilverByTech), nameof(GiveItemsPrefix)),
                postfix: new HarmonyMethod(typeof(GuestSilverByTech), nameof(GiveItemsPostfix)));
        }

        public static void GiveItemsPrefix(IEnumerable<Pawn> visitors)
        {
            silverBefore.Clear();
            if (visitors == null)
                return;

            foreach (var pawn in visitors)
            {
                if (pawn?.inventory != null)
                    silverBefore[pawn] = pawn.inventory.innerContainer.TotalStackCountOfDef(ThingDefOf.Silver);
            }
        }

        public static void GiveItemsPostfix(IEnumerable<Pawn> visitors)
        {
            try
            {
                var multipliers = HardcoreSettingsDef.Instance?.guestSilverMultiplierByTech;
                if (visitors == null || multipliers == null || multipliers.Count == 0)
                    return;

                foreach (var pawn in visitors)
                {
                    if (pawn?.inventory == null || pawn.Faction?.def == null)
                        continue;
                    if (!multipliers.TryGetValue(pawn.Faction.def.techLevel, out float multiplier) || multiplier <= 1f)
                        continue;

                    silverBefore.TryGetValue(pawn, out int before);
                    int given = pawn.inventory.innerContainer.TotalStackCountOfDef(ThingDefOf.Silver) - before;
                    if (given <= 0)
                        continue;

                    int extra = Mathf.RoundToInt(given * (multiplier - 1f));
                    if (extra <= 0)
                        continue;

                    AddSilver(pawn, extra);
                }
            }
            finally
            {
                silverBefore.Clear();
            }
        }

        private static void AddSilver(Pawn pawn, int count)
        {
            var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
            silver.stackCount = count;

            int space = getInventorySpaceFor(pawn, silver);
            if (space <= 0)
            {
                silver.Destroy();
                return;
            }

            silver.stackCount = Mathf.Min(space, count);
            int added = silver.stackCount;
            if (!pawn.inventory.innerContainer.TryAdd(silver))
            {
                if (!silver.Destroyed)
                    silver.Destroy();
                return;
            }

            if (Prefs.DevMode)
                Log.Message($"[HSKMoreHardcore] GuestSilverByTech: {pawn.LabelShort} ({pawn.Faction.Name}, тех {pawn.Faction.def.techLevel}) +{added} серебра.");
        }
    }
}
