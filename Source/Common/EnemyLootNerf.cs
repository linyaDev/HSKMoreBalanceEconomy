using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKMoreHardcore
{
    [StaticConstructorOnStartup]
    public static class EnemyLootNerf
    {
        private static Type ammoThingType;

        static EnemyLootNerf()
        {
            ammoThingType = AccessTools.TypeByName("CombatExtended.AmmoThing");

            var harmony = new Harmony("linya.hskmorehardcore.enemylootnerf");

            // Нерф расходников/оружия из инвентаря — на универсальном GenDrop.TryDropSpawn,
            // чтобы ловить все пути дропа (раздеть всё, выборочно через NonUnoPinata, смерть).
            var tryDropSpawn = AccessTools.Method(typeof(GenDrop), "TryDropSpawn");
            if (tryDropSpawn != null)
            {
                harmony.Patch(tryDropSpawn,
                    prefix: new HarmonyMethod(typeof(EnemyLootNerf), nameof(TryDropSpawnPrefix)));
                Log.Message("[HSKMoreHardcore] EnemyLootNerf applied.");
            }

            // Помечаем одежду при раздевании вражеской пешки
            var apparelTryDrop = AccessTools.Method(typeof(Pawn_ApparelTracker), "TryDrop",
                new Type[] { typeof(RimWorld.Apparel), typeof(RimWorld.Apparel).MakeByRefType(), typeof(IntVec3), typeof(bool) });
            if (apparelTryDrop != null)
            {
                harmony.Patch(apparelTryDrop,
                    postfix: new HarmonyMethod(typeof(EnemyLootNerf), nameof(ApparelDropPostfix)));
                Log.Message("[HSKMoreHardcore] EnemyLootNerf (apparel worn mark) applied.");
            }

            // Помечаем оружие, которое выпало из рук вражеской пешки (смерть, опрокидывание,
            // «раздеть всё», разоружение). Оружие колонистов не метим: они постоянно
            // перекладывают стволы, и метка вешалась бы на ровном месте.
            var equipmentTryDrop = AccessTools.Method(typeof(Pawn_EquipmentTracker), "TryDropEquipment",
                new Type[] { typeof(ThingWithComps), typeof(ThingWithComps).MakeByRefType(), typeof(IntVec3), typeof(bool) });
            if (equipmentTryDrop != null)
            {
                harmony.Patch(equipmentTryDrop,
                    postfix: new HarmonyMethod(typeof(EnemyLootNerf), nameof(EquipmentDropPostfix)));
                Log.Message("[HSKMoreHardcore] EnemyLootNerf (weapon battle-worn mark) applied.");
            }

            var genInv = AccessTools.Method(typeof(RimWorld.PawnInventoryGenerator), "GenerateInventoryFor");
            if (genInv != null)
            {
                harmony.Patch(genInv,
                    postfix: new HarmonyMethod(typeof(EnemyLootNerf), nameof(InventoryGenPostfix)));
                Log.Message("[HSKMoreHardcore] EnemyLootNerf (inventory gen) applied.");
            }
        }

        // Помечаем одежду при раздевании вражеской пешки
        public static void ApparelDropPostfix(Pawn_ApparelTracker __instance, RimWorld.Apparel ap, RimWorld.Apparel resultingAp)
        {
            var droppedApparel = resultingAp ?? ap;
            if (droppedApparel == null)
                return;

            var pawn = __instance.pawn;

            // Добавляем комп если нет
            var comp = droppedApparel.TryGetComp<CompWornByEnemy>();
            if (comp == null)
            {
                comp = new CompWornByEnemy();
                comp.parent = droppedApparel;
                droppedApparel.AllComps.Add(comp);
            }

            if (pawn != null && pawn.Faction != null && pawn.Faction.IsPlayer)
            {
                comp.worn = true;
            }
            else
            {
                comp.wornByEnemy = true;
            }
        }

        // Оружие из рук вражеской пешки -> метка «со следами боя»
        public static void EquipmentDropPostfix(Pawn_EquipmentTracker __instance, ThingWithComps eq, ThingWithComps resultingEq)
        {
            var dropped = resultingEq ?? eq;
            if (dropped == null || dropped.def == null || !dropped.def.IsWeapon)
                return;

            MarkWeaponWornByEnemy(dropped, __instance.pawn);
        }

        // Ставит метку «со следами боя», если оружие выпало из враждебной (не игроковой) пешки.
        public static void MarkWeaponWornByEnemy(Thing weapon, Pawn pawn)
        {
            if (pawn == null || pawn.Faction == null || pawn.Faction.IsPlayer
                || pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony)
                return;

            var comp = weapon.TryGetComp<CompWornByEnemy>();
            if (comp == null)
            {
                var twc = weapon as ThingWithComps;
                if (twc == null)
                    return;
                comp = new CompWornByEnemy();
                comp.parent = twc;
                twc.AllComps.Add(comp);
            }

            comp.wornByEnemy = true;
        }

        public static void InventoryGenPostfix(Pawn p)
        {
            try
            {
                if (p == null || (p.Faction != null && p.Faction.IsPlayer))
                    return;

                // Не трогать торговцев
                if (p.kindDef?.trader == true)
                    return;

                // Замену индустриальной медицины на травы делает XML-патч пешкокайндов
                // (Patches/Core_SK/RaiderMedicineHerbal.xml).
            }
            catch (Exception e)
            {
                Log.Error($"[EnemyLootNerf] InventoryGenPostfix error on {p?.LabelShort}: {e}");
            }
        }

        // Срабатывает на ЛЮБОМ дропе вещи; нерфим только то, что падает из инвентаря вражеской пешки.
        // Это ловит все пути: «раздеть всё» (DropAllNearPawn), выборочно (NonUnoPinata innerContainer.TryDrop), смерть.
        public static void TryDropSpawnPrefix(Thing thing, Map map)
        {
            if (thing == null)
                return;

            // Только инвентарь вражеской пешки (не игрок/пленные/рабы). Экипировка/одежда сюда не попадают.
            var pawn = (thing.ParentHolder as Pawn_InventoryTracker)?.pawn;
            if (pawn == null || pawn.Faction == null || pawn.Faction.IsPlayer
                || pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony)
                return;

            bool isAwayMap = map != null && !map.IsPlayerHome;
            string tag = isAwayMap ? "away" : "home";

            if (thing.def.IsMedicine)
            {
                float medMult = isAwayMap ? NerfSettings.medicineDropMultiplierAway : NerfSettings.medicineDropMultiplier;
                int before = thing.stackCount;
                thing.stackCount = Mathf.Max(1, Mathf.FloorToInt(thing.stackCount * medMult));
            }
            else if (ammoThingType != null && ammoThingType.IsInstanceOfType(thing))
            {
                float ammoMult = isAwayMap ? NerfSettings.ammoDropMultiplierAway : NerfSettings.ammoDropMultiplier;
                int before = thing.stackCount;
                thing.stackCount = Mathf.Max(1, Mathf.FloorToInt(thing.stackCount * ammoMult));
            }
            else if (thing.def.IsDrug && thing.stackCount > 1)
            {
                int before = thing.stackCount;
                thing.stackCount = Mathf.Max(1, Mathf.FloorToInt(thing.stackCount * NerfSettings.drugDropMultiplier));
            }
            else if (thing is RimWorld.Apparel apparel)
            {
                // Одежда в инвентаре врага (в т.ч. груз вьючных животных): метка «со следами боя»
                var comp = apparel.TryGetComp<CompWornByEnemy>();
                if (comp != null)
                    comp.wornByEnemy = true;
            }
            else if (thing.def.IsWeapon)
            {
                // Запасное оружие в инвентаре врага (сайдармы, груз вьючных животных)
                MarkWeaponWornByEnemy(thing, pawn);
            }
        }
    }
}
