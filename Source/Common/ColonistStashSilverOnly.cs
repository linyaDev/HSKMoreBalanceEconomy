using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    // Событие Core_SK "ColonistStash" ("заначка поселенца", письмо FoundTreasure) по умолчанию
    // даёт либо случайный предмет/оружие, либо серебро. Оставляем только серебро:
    // префикс полностью заменяет воркер на серебряную ветку.
    [StaticConstructorOnStartup]
    public static class ColonistStashSilverOnly
    {
        static ColonistStashSilverOnly()
        {
            var method = AccessTools.Method("SK.Events.IncidentWorker_ColonistStash:TryExecuteWorker");
            if (method == null)
                return;

            var harmony = new Harmony("linya.hskmorehardcore.coloniststash");
            harmony.Patch(method,
                prefix: new HarmonyMethod(typeof(ColonistStashSilverOnly), nameof(Prefix)));
            Log.Message("[HSKMoreHardcore] ColonistStashSilverOnly applied.");
        }

        // Полная замена награды на серебро (как в серебряной ветке оригинала)
        public static bool Prefix(IncidentParms parms, ref bool __result)
        {
            __result = true;

            if (!(parms.target is Map map))
                return false; // скипаем оригинал в любом случае

            // Случайный свободный колонист (взрослый или ребёнок), иначе любой свободный
            if (!map.mapPawns.FreeColonists
                    .Where(c => c.DevelopmentalStage == DevelopmentalStage.Adult
                             || c.DevelopmentalStage == DevelopmentalStage.Child)
                    .TryRandomElement(out Pawn pawn)
                && !map.mapPawns.FreeColonists.TryRandomElement(out pawn))
            {
                return false; // некому — событие ничего не делает
            }

            var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
            silver.stackCount = Rand.Range(100, 400);
            GenPlace.TryPlaceThing(silver, pawn.Position, map, ThingPlaceMode.Near);
            silver.SetForbidden(true, true);

            string money = (pawn.gender == Gender.Male ? "HeMoney" : "SheMoney").Translate();
            string body = pawn.LabelShort + money + silver.stackCount + (string)"SilverStash".Translate();
            Find.LetterStack.ReceiveLetter("FoundTreasure".Translate(), body, LetterDefOf.PositiveEvent, pawn);
            return false;
        }
    }
}
