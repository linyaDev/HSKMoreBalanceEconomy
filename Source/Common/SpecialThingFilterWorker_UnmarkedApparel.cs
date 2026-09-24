using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    // Совпадает с одеждой БЕЗ метки износа («Ношеное»/«Со следами боя»).
    // В рецепте стирки этот фильтр запрещён -> в биле остаётся только помеченная одежда.
    public class SpecialThingFilterWorker_UnmarkedApparel : SpecialThingFilterWorker
    {
        public override bool Matches(Thing t)
        {
            var comp = (t as ThingWithComps)?.GetComp<CompWornByEnemy>();
            return comp == null || !comp.IsWornMarked;
        }

        public override bool CanEverMatch(ThingDef def)
        {
            return def.IsApparel;
        }
    }
}
