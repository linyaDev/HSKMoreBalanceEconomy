using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKMoreHardcore
{
    [StaticConstructorOnStartup]
    public static class TraderAmmoNerf
    {
        private static Type ammoThingType;

        static TraderAmmoNerf()
        {
            ammoThingType = AccessTools.TypeByName("CombatExtended.AmmoThing");
            if (ammoThingType == null)
            {
                Log.Warning("[HSKMoreHardcore] TraderAmmoNerf: CombatExtended.AmmoThing not found.");
                return;
            }

            var harmony = new Harmony("linya.hskmorehardcore.traderammo");

            // Патч на количество патронов у торговцев
            var generatePublic = AccessTools.Method(typeof(ThingSetMaker), "Generate",
                new Type[] { typeof(ThingSetMakerParams) });
            if (generatePublic != null)
            {
                harmony.Patch(generatePublic,
                    postfix: new HarmonyMethod(typeof(TraderAmmoNerf), nameof(StockPostfix)));
                Log.Message("[HSKMoreHardcore] TraderAmmoNerf (stock) applied.");
            }

            // Патч на цену покупки у торговца
            var getPriceFor = AccessTools.Method(typeof(Tradeable), "GetPriceFor");
            if (getPriceFor != null)
            {
                harmony.Patch(getPriceFor,
                    postfix: new HarmonyMethod(typeof(TraderAmmoNerf), nameof(PricePostfix)));
                Log.Message("[HSKMoreHardcore] TraderAmmoNerf (price) applied.");
            }
        }

        private static Type traderStockType;
        private static Type rewardMarketValueType;

        public static void StockPostfix(ThingSetMaker __instance, ref List<Thing> __result)
        {
            if (traderStockType == null)
                traderStockType = typeof(ThingSetMaker).Assembly.GetType("RimWorld.ThingSetMaker_TraderStock");
            if (rewardMarketValueType == null)
                rewardMarketValueType = typeof(ThingSetMaker).Assembly.GetType("RimWorld.ThingSetMaker_MarketValue");

            bool isTrader = traderStockType != null && traderStockType.IsInstanceOfType(__instance);
            bool isReward = rewardMarketValueType != null && rewardMarketValueType.IsInstanceOfType(__instance);

            if (!isTrader && !isReward)
                return;

            var settings = HardcoreSettingsDef.Instance;
            int silverMin = settings?.traderSilverMinimum ?? 3500;

            for (int i = __result.Count - 1; i >= 0; i--)
            {
                var thing = __result[i];
                if (isTrader && thing.def == ThingDefOf.Silver && thing.stackCount < silverMin)
                {
                    thing.stackCount = silverMin;
                }
                else if (ammoThingType.IsInstanceOfType(thing) && thing.stackCount > 1)
                {
                    float mult = isTrader ? NerfSettings.traderAmmoMultiplier : NerfSettings.rewardAmmoMultiplier;
                    thing.stackCount = Mathf.Max(5, Mathf.FloorToInt(thing.stackCount * mult));
                }
            }
        }

        public static void PricePostfix(Tradeable __instance, TradeAction action, ref float __result)
        {
            var thing = __instance.AnyThing;
            if (thing == null)
                return;

            if (action == TradeAction.PlayerBuys)
            {
                // Правила цены покупки действуют у любого торговца
                var buySettings = HardcoreSettingsDef.Instance;
                if (buySettings != null)
                {
                    float buyMult = GetBuyMultiplier(thing.def, buySettings);
                    if (buyMult != 1f)
                        __result *= buyMult;
                }

                // Наценка на патроны/оружие только при торговле в поселениях
                // (и с орбитой): караван, пришедший к нам на карту, торгует
                // без наценки — TradeSession.trader тогда пешка.
                if (TradeSession.trader is Pawn)
                    return;

                if (ammoThingType != null && ammoThingType.IsInstanceOfType(thing))
                {
                    __result *= NerfSettings.ammoPriceMultiplier;
                }
                else if (thing.def.IsWeapon)
                {
                    __result *= NerfSettings.weaponPriceMultiplier;
                }
            }
            else if (action == TradeAction.PlayerSells)
            {
                var settings = HardcoreSettingsDef.Instance;
                if (settings == null)
                    return;

                // Животные не из колонии дешевле при продаже: пришедшие событием
                // и прирученные вручную. Рождённые в колонии метки не имеют.
                if (thing is Pawn pawn && pawn.RaceProps != null && pawn.RaceProps.Animal)
                {
                    var mark = pawn.TryGetComp<CompFreeAnimal>();
                    if (mark != null)
                    {
                        if (mark.joinedFree && settings.freeAnimalSellMultiplier != 1f)
                        {
                            __result *= settings.freeAnimalSellMultiplier;
                            return;
                        }
                        if (mark.tamed && settings.tamedAnimalSellMultiplier != 1f)
                        {
                            __result *= settings.tamedAnimalSellMultiplier;
                            return;
                        }
                    }
                }

                float mult = GetSellMultiplier(thing.def, settings);
                if (mult != 1f)
                    __result *= mult;
            }
        }

        // Множитель цены для дефа: точечный из overrides, иначе первое подходящее
        // правило из списка. Результат кэшируется отдельно для каждой стороны сделки.
        private static readonly Dictionary<ThingDef, float> sellMultCache = new Dictionary<ThingDef, float>();
        private static readonly Dictionary<ThingDef, float> buyMultCache = new Dictionary<ThingDef, float>();

        private static float GetSellMultiplier(ThingDef def, HardcoreSettingsDef settings)
        {
            return GetMultiplier(def, settings.sellPriceOverrides, settings.sellPriceRules, sellMultCache, "продажи");
        }

        private static float GetBuyMultiplier(ThingDef def, HardcoreSettingsDef settings)
        {
            return GetMultiplier(def, settings.buyPriceOverrides, settings.buyPriceRules, buyMultCache, "покупки");
        }

        private static float GetMultiplier(ThingDef def, Dictionary<string, float> overrides,
            List<PriceRule> rules, Dictionary<ThingDef, float> cache, string what)
        {
            if (def == null)
                return 1f;

            if (cache.TryGetValue(def, out float cached))
                return cached;

            float result = 1f;

            if (overrides != null && overrides.TryGetValue(def.defName, out float over))
            {
                result = over;
            }
            else if (rules != null)
            {
                foreach (var rule in rules)
                {
                    if (rule == null)
                        continue;
                    if (!rule.HasAnySelector)
                    {
                        Log.WarningOnce($"[HSKMoreHardcore] Правило цены {what} \"{rule.label ?? "без имени"}\" без признаков отбора — пропущено.",
                            (rule.label ?? "noname").GetHashCode() ^ what.GetHashCode());
                        continue;
                    }
                    if (rule.Matches(def))
                    {
                        result = rule.multiplier;
                        break;
                    }
                }
            }

            cache[def] = result;
            return result;
        }
    }
}
