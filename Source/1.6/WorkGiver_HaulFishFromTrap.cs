using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HSKMoreHardcore
{
    // Отдельная работа в колонке «рыба» (WorkTypeDef Fishing из Odyssey): унести
    // улов из рыбной ловушки на склад.
    //
    // Ловушка HSK_FishTrap ловит рыбу сама (SK.CompFishTrap спавнит рыбу или труп
    // прямо в свою клетку), но сама она — хранилище LWM Deep Storage. Для обычного
    // грузчика улов уже «сложен», и он унесёт его только в хранилище с более
    // высоким приоритетом. Поэтому рыба копится в ловушке, пока её не заберут руками.
    //
    // Наш воркгивер ищет именно улов в клетках ловушек и уносит в любое подходящее
    // хранилище, не требуя, чтобы оно было приоритетнее самой ловушки.
    public class WorkGiver_HaulFishFromTrap : WorkGiver_Scanner
    {
        private static System.Type fishTrapCompType;
        private static bool typeResolved;

        public override PathEndMode PathEndMode => PathEndMode.ClosestTouch;

        public override Danger MaxPathDanger(Pawn pawn) => Danger.Deadly;

        private static System.Type FishTrapCompType
        {
            get
            {
                if (!typeResolved)
                {
                    fishTrapCompType = AccessTools.TypeByName("SK.CompFishTrap");
                    typeResolved = true;
                }
                return fishTrapCompType;
            }
        }

        // Улов лежит в клетках ловушек, поэтому список собираем от построек, а не
        // сканируем все переносимые вещи на карте
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            var compType = FishTrapCompType;
            if (compType == null || pawn?.Map == null)
                yield break;

            var buildings = pawn.Map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (!IsFishTrap(building, compType))
                    continue;

                foreach (var cell in building.OccupiedRect())
                {
                    var things = pawn.Map.thingGrid.ThingsListAtFast(cell);
                    for (int j = 0; j < things.Count; j++)
                    {
                        var t = things[j];
                        if (t != null && t != building && t.def.EverHaulable)
                            yield return t;
                    }
                }
            }
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return FishTrapCompType == null;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobOnThing(pawn, t, forced) != null;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t == null || !t.Spawned || t.Map == null || !t.def.EverHaulable)
                return null;

            var compType = FishTrapCompType;
            if (compType == null || !StandsOnFishTrap(t, compType))
                return null;

            if (!HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, t, forced))
                return null;

            // Unstored: годится любое хранилище, а не только приоритетнее ловушки
            if (!StoreUtility.TryFindBestBetterStoreCellFor(t, pawn, t.Map, StoragePriority.Unstored,
                    pawn.Faction, out IntVec3 storeCell))
                return null;

            return HaulAIUtility.HaulToCellStorageJob(pawn, t, storeCell, fitInStoreCell: false);
        }

        private static bool StandsOnFishTrap(Thing t, System.Type compType)
        {
            var things = t.Map.thingGrid.ThingsListAtFast(t.Position);
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i] is Building b && IsFishTrap(b, compType))
                    return true;
            }
            return false;
        }

        private static bool IsFishTrap(Building building, System.Type compType)
        {
            if (building?.AllComps == null)
                return false;

            var comps = building.AllComps;
            for (int i = 0; i < comps.Count; i++)
            {
                if (compType.IsInstanceOfType(comps[i]))
                    return true;
            }
            return false;
        }
    }
}
