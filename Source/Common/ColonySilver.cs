using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKMoreHardcore
{
    // Плата серебром за наши услуги (сигнальный костёр, заказ каравана
    // с консоли связи). Ванильный TradeUtility берёт серебро только из зоны
    // запитанного орбитального маяка — нам нужно всё, что лежит на карте.
    public static class ColonySilver
    {
        public static int CountOnMap(Map map)
        {
            if (map == null)
                return 0;

            int total = 0;
            foreach (Thing t in map.listerThings.ThingsOfDef(ThingDefOf.Silver))
                total += t.stackCount;
            return total;
        }

        public static bool TakeFromMap(Map map, int amount)
        {
            if (map == null)
                return false;

            int remaining = amount;
            List<Thing> silvers = map.listerThings.ThingsOfDef(ThingDefOf.Silver).ToList();
            foreach (Thing silver in silvers)
            {
                if (remaining <= 0) break;
                int take = Mathf.Min(silver.stackCount, remaining);
                silver.SplitOff(take).Destroy();
                remaining -= take;
            }
            return remaining <= 0;
        }
    }
}
