using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    // Тепличный торговец (SeedsPlease) скупает семена в 10 раз дороже остальных.
    // SellPriceFactor 0.05 задан на самих семенах и действует у любого скупщика,
    // поторговцевых ценовых множителей в движке нет — поднимаем итоговую цену
    // постфиксом на GetPriceFor только в его торговой сессии (0.05 x10 = 50%).
    [StaticConstructorOnStartup]
    public static class SeedTraderPrice
    {
        private const float SeedSellMultiplier = 10f;

        static SeedTraderPrice()
        {
            var getPriceFor = AccessTools.Method(typeof(Tradeable), "GetPriceFor");
            if (getPriceFor == null)
            {
                Log.Warning("[HSKMoreHardcore] SeedTraderPrice: Tradeable.GetPriceFor not found.");
                return;
            }

            var harmony = new Harmony("linya.hskmorehardcore.seedtraderprice");
            harmony.Patch(getPriceFor,
                postfix: new HarmonyMethod(typeof(SeedTraderPrice), nameof(PricePostfix)));
            Log.Message("[HSKMoreHardcore] SeedTraderPrice applied.");
        }

        public static void PricePostfix(Tradeable __instance, TradeAction action, ref float __result)
        {
            if (action != TradeAction.PlayerSells)
                return;

            var kind = TradeSession.trader?.TraderKind;
            if (kind == null)
                return;
            if (kind.defName != "GreenHouseTrader" && kind.defName != "Caravan_GreenHouseTrader")
                return;

            var thing = __instance.AnyThing;
            if (thing?.def?.tradeTags != null && thing.def.tradeTags.Contains("Seeds"))
                __result *= SeedSellMultiplier;
        }
    }
}
