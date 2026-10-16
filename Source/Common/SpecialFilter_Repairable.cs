using Verse;

namespace HSKMoreHardcore
{
    public class SpecialThingFilterWorker_AllowRepairable : SpecialThingFilterWorker
    {
        public override bool Matches(Thing t)
        {
            var comp = t.TryGetComp<CompWornByEnemy>();
            if (comp == null)
                return true; // нет компа — считаем восстанавливаемым
            return comp.CanMendNormally;
        }

        public override bool CanEverMatch(ThingDef def)
        {
            return def.IsApparel || def.IsWeapon;
        }

        public override bool AlwaysMatches(ThingDef def)
        {
            return false;
        }
    }

    // Обратный фильтр: вещи с исчерпанной обычной починкой (repairCount >= порога).
    // Спец-фильтры умеют только исключать (снятая галочка), поэтому для «склада
    // только чинибельного» нужен этот парный фильтр — как ванильные fresh/rotten.
    public class SpecialThingFilterWorker_AllowMendExhausted : SpecialThingFilterWorker
    {
        public override bool Matches(Thing t)
        {
            var comp = t.TryGetComp<CompWornByEnemy>();
            if (comp == null)
                return false;
            return !comp.CanMendNormally;
        }

        public override bool CanEverMatch(ThingDef def)
        {
            return def.IsApparel || def.IsWeapon;
        }

        public override bool AlwaysMatches(ThingDef def)
        {
            return false;
        }
    }
}
