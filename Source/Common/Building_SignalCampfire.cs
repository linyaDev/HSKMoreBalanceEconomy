using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    public class Building_SignalCampfire : Building, IThingGlower
    {
        // CompGlower уважает IThingGlower: светим только когда сигнал зажжён (активен)
        public bool ShouldBeLitNow()
        {
            var comp = GetComp<CompTradeSignal>();
            return comp != null && comp.IsActive;
        }

#if V16
        public override AcceptanceReport DeconstructibleBy(Faction faction)
#else
        public override bool DeconstructibleBy(Faction faction)
#endif
        {
            var comp = GetComp<CompTradeSignal>();
            if (comp != null && comp.IsActive)
                return false;

            return base.DeconstructibleBy(faction);
        }
    }
}
