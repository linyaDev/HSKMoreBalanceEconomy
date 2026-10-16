using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace HSKMoreHardcore
{
    [StaticConstructorOnStartup]
    public static class PodLootNerf
    {
        private static Type ammoThingType;
        private static IntVec3 savedPosition;
        private static HashSet<int> existingThingIds = new HashSet<int>();

        static PodLootNerf()
        {
            ammoThingType = AccessTools.TypeByName("CombatExtended.AmmoThing");

            var spawnLoot = AccessTools.Method("SK.CompLoot:SpawnLoot");
            if (spawnLoot == null)
                return;

            var harmony = new Harmony("linya.hskmorehardcore.podlootnerf");
            harmony.Patch(spawnLoot,
                prefix: new HarmonyMethod(typeof(PodLootNerf), nameof(Prefix)),
                postfix: new HarmonyMethod(typeof(PodLootNerf), nameof(Postfix)));
            Log.Message("[HSKMoreHardcore] PodLootNerf applied.");
        }

        public static void Prefix(object __instance, Map map)
        {
            var parentField = AccessTools.Field(__instance.GetType(), "parent");
            var parent = parentField?.GetValue(__instance) as Thing;
            if (parent == null)
                return;

            savedPosition = parent.Position;

            // Remember existing things near pod before loot spawns
            existingThingIds.Clear();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(savedPosition, 3f, true))
            {
                if (!cell.InBounds(map))
                    continue;
                foreach (Thing t in map.thingGrid.ThingsListAt(cell))
                {
                    existingThingIds.Add(t.thingIDNumber);
                }
            }
        }

        public static void Postfix(Map map)
        {
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(savedPosition, 3f, true))
            {
                if (!cell.InBounds(map))
                    continue;

                var things = map.thingGrid.ThingsListAt(cell);
                for (int i = things.Count - 1; i >= 0; i--)
                {
                    var thing = things[i];

                    // Only nerf newly spawned things
                    if (existingThingIds.Contains(thing.thingIDNumber))
                        continue;

                    // Убираем вещи из запрещённых материалов
                    if (thing.Stuff != null && NerfSettings.bannedPodMaterials.Contains(thing.Stuff.defName))
                    {
                        thing.Destroy();
                        continue;
                    }

                    if (thing.def.IsMedicine && thing.stackCount > 1)
                    {
                        thing.stackCount = Mathf.Max(1, Mathf.FloorToInt(thing.stackCount * NerfSettings.medicineDropMultiplier));
                    }
                    else if (ammoThingType != null && ammoThingType.IsInstanceOfType(thing) && thing.stackCount > 1)
                    {
                        thing.stackCount = Mathf.Max(1, Mathf.FloorToInt(thing.stackCount * NerfSettings.podAmmoMultiplier));
                    }
                    else if (thing.def.IsDrug && thing.stackCount > 1)
                    {
                        thing.stackCount = Mathf.Max(1, Mathf.FloorToInt(thing.stackCount * NerfSettings.podDrugMultiplier));
                    }
                    else if (NerfSettings.removePodWeapons && thing.def.IsWeapon)
                    {
                        thing.Destroy();
                    }
                    else if (thing is RimWorld.Apparel apparel)
                    {
                        // Нерф прочности брони из капсул/ящиков (по защите и нашему уровню развития)
                        ArmorLootNerf.Apply(apparel, "pod");
                    }
                }
            }

            existingThingIds.Clear();
        }
    }
}
