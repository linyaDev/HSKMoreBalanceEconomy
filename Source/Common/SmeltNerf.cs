using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace HSKMoreHardcore
{
    // Разбор оружия (SmeltWeapon, DissasembleFirearms и всё, что идёт через
    // specialProducts Smelted) в паке возвращает 25% costList, а там лежат
    // дорогие детали оружия — разобрать трофей выгоднее, чем сделать деталь.
    //
    // Теперь решает целостность ствола: целее порога
    // (weaponPartSalvageMinHitPct) — отдаёт половину деталей, ниже — только
    // металл. Что не уцелело, даёт стальные прутки, как было раньше.
    //
    // Патроны из разобранного оружия к нам отношения не имеют: их выкидывает
    // Combat Extended из магазина, и мы их не трогаем.
    [StaticConstructorOnStartup]
    public static class SmeltNerf
    {
        private static HashSet<string> bannedSmeltProducts;

        static SmeltNerf()
        {
            bannedSmeltProducts = new HashSet<string>
            {
                "Weapon_Parts",
                "Pistol_Component",
                "SMG_Component",
                "Shotgun_Component",
                "Rifle_Component",
                "AdvRifle_Component",
                "Sniper_Component",
                "AdvSniper_Component",
                "Heavy_Component",
                "Cannon_Component",
                "Launcher_Component",
                "Charged_Component",
                "Plasma_Component",
                "Laser_Component"
            };

            var harmony = new Harmony("linya.hskmorehardcore.smeltnerf");

            var smeltProducts = AccessTools.Method(typeof(Thing), "SmeltProducts");
            if (smeltProducts != null)
            {
                harmony.Patch(smeltProducts,
                    postfix: new HarmonyMethod(typeof(SmeltNerf), nameof(Postfix)));
                Log.Message("[HSKMoreHardcore] SmeltNerf applied.");
            }
        }

        private static ThingDef steelBarDef;

        public static IEnumerable<Thing> Postfix(IEnumerable<Thing> __result, Thing __instance)
        {
            var settings = HardcoreSettingsDef.Instance;
            float keptFraction = PartKeptFraction(__instance, settings);
            int steelPerSmelt = settings?.weaponPartSmeltSteel ?? 5;

            float otherFraction = settings?.weaponSmeltOtherFraction ?? 1f;

            bool replaced = false;
            foreach (var thing in __result)
            {
                if (!bannedSmeltProducts.Contains(thing.def.defName))
                {
                    // Патроны выкидывает Combat Extended из магазина — не наше
                    if (IsAmmo(thing) || otherFraction >= 1f)
                    {
                        yield return thing;
                        continue;
                    }

                    int left = GenMath.RoundRandom(thing.stackCount * otherFraction);
                    if (left <= 0)
                        continue;

                    thing.stackCount = left;
                    yield return thing;
                    continue;
                }

                // Дробный остаток решается броском: с одной детали это
                // просто шанс в половину случаев
                int kept = GenMath.RoundRandom(thing.stackCount * keptFraction);

                if (kept > 0)
                {
                    thing.stackCount = kept;
                    yield return thing;
                    continue;
                }

                if (!replaced && steelPerSmelt > 0)
                {
                    if (steelBarDef == null)
                        steelBarDef = DefDatabase<ThingDef>.GetNamedSilentFail("SteelBar");

                    if (steelBarDef != null)
                    {
                        Thing steel = ThingMaker.MakeThing(steelBarDef);
                        steel.stackCount = steelPerSmelt;
                        yield return steel;
                    }
                    replaced = true;
                }
            }
        }

        private static System.Type ammoThingType;
        private static bool ammoTypeChecked;

        // Патроны Combat Extended — отдельный класс, дефом их не перечислить
        private static bool IsAmmo(Thing thing)
        {
            if (!ammoTypeChecked)
            {
                ammoThingType = AccessTools.TypeByName("CombatExtended.AmmoThing");
                ammoTypeChecked = true;
            }
            return ammoThingType != null && ammoThingType.IsInstanceOfType(thing);
        }

        // Доля деталей, которая уцелеет: ствол целее порога отдаёт свою долю
        // (по умолчанию половину), потрёпанный — ничего, только металл.
        private static float PartKeptFraction(Thing weapon, HardcoreSettingsDef settings)
        {
            float fraction = settings?.weaponPartSalvageFraction ?? 0f;
            if (fraction <= 0f)
                return 0f;

            float minPct = settings?.weaponPartSalvageMinHitPct ?? 1f;
            if (weapon == null || !weapon.def.useHitPoints || weapon.MaxHitPoints <= 0)
                return Mathf.Clamp01(fraction);

            float pct = (float)weapon.HitPoints / weapon.MaxHitPoints;
            if (pct <= minPct)
                return 0f;

            return Mathf.Clamp01(fraction);
        }
    }
}
