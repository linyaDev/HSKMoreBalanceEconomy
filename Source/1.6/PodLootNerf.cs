using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKMoreHardcore
{
    // Нерф лута капсул. В Core_SK 1.6 лут генерируется в контейнер капсулы при
    // приземлении (CapsuleLootGenerator.InitializeLoot), а вскрытие лишь
    // выкладывает готовое содержимое — поэтому режем прямо в контейнере после
    // генерации, а не сканируем карту, как делали на 1.5 (SK.CompLoot больше нет).
    [StaticConstructorOnStartup]
    public static class PodLootNerf
    {
        private static Type ammoThingType;

        static PodLootNerf()
        {
            ammoThingType = AccessTools.TypeByName("CombatExtended.AmmoThing");

            var initializeLoot = AccessTools.Method("SK.CapsuleLootGenerator:InitializeLoot");
            if (initializeLoot == null)
            {
                Log.Warning("[HSKMoreHardcore] PodLootNerf: SK.CapsuleLootGenerator.InitializeLoot not found, pod loot nerf disabled.");
                return;
            }

            var harmony = new Harmony("linya.hskmorehardcore.podlootnerf");
            harmony.Patch(initializeLoot,
                prefix: new HarmonyMethod(typeof(PodLootNerf), nameof(Prefix)),
                postfix: new HarmonyMethod(typeof(PodLootNerf), nameof(Postfix)));
            Log.Message("[HSKMoreHardcore] PodLootNerf applied.");
        }

        // InitializeLoot генерирует лут только в пустой (без не-пешек) контейнер;
        // если груз уже есть — это чужие/игроковые вещи, их не трогаем.
        public static void Prefix(ActiveTransporterInfo cargo, out bool __state)
        {
            __state = true;
            foreach (Thing t in cargo.innerContainer)
            {
                if (!(t is Pawn))
                {
                    __state = false;
                    break;
                }
            }
        }

        public static void Postfix(ActiveTransporterInfo cargo, bool __state)
        {
            if (!__state)
                return;

            var container = cargo.innerContainer;
            for (int i = container.Count - 1; i >= 0; i--)
            {
                Thing thing = container[i];
                if (thing is Pawn)
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
                else if (thing is Apparel apparel)
                {
                    // Нерф прочности брони из капсул/ящиков (по защите и нашему уровню развития)
                    ArmorLootNerf.Apply(apparel, "pod");
                }
            }
        }
    }
}
