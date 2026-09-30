using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    /// <summary>
    /// «Слишком глубоко: заражение» (DeepDrillInfestation) срабатывает только
    /// у буров и добытчиков, стоящих под твёрдой крышей (нависающая скала,
    /// isThickRoof). На открытом месте или под обычной крышей жуки от бурения
    /// не лезут. Постфикс на CompCreatesInfestations.CanCreateInfestationNow —
    /// через него идёт и подсчёт частоты у рассказчика (MTB на каждый активный
    /// бур), и отбор целей самим инцидентом.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class DrillInfestationThickRoof
    {
        static DrillInfestationThickRoof()
        {
            var getter = AccessTools.PropertyGetter(typeof(CompCreatesInfestations),
                nameof(CompCreatesInfestations.CanCreateInfestationNow));
            if (getter == null)
            {
                Log.Error("[HSKMoreHardcore] DrillInfestationThickRoof: CanCreateInfestationNow not found.");
                return;
            }

            new Harmony("linya.hskmorehardcore.drillinfestationroof").Patch(getter,
                postfix: new HarmonyMethod(typeof(DrillInfestationThickRoof), nameof(Postfix)));
            Log.Message("[HSKMoreHardcore] DrillInfestationThickRoof applied: заражение от бурения только под твёрдой крышей.");
        }

        public static void Postfix(CompCreatesInfestations __instance, ref bool __result)
        {
            if (!__result)
                return;
            if (!AnyOccupiedCellUnderThickRoof(__instance.parent))
                __result = false;
        }

        private static bool AnyOccupiedCellUnderThickRoof(Thing thing)
        {
            var map = thing?.Map;
            if (map == null || !thing.Spawned)
                return false;

            foreach (var cell in thing.OccupiedRect())
            {
                if (cell.InBounds(map) && cell.GetRoof(map)?.isThickRoof == true)
                    return true;
            }
            return false;
        }
    }
}
