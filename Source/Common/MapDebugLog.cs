using System.Linq;
using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace HSKMoreHardcore
{
    [StaticConstructorOnStartup]
    public static class MapDebugLog
    {
        static MapDebugLog()
        {
            var harmony = new Harmony("linya.hskmorehardcore.mapdebuglog");

            var finalizeInit = AccessTools.Method(typeof(Map), "FinalizeInit");
            if (finalizeInit != null)
            {
                harmony.Patch(finalizeInit,
                    postfix: new HarmonyMethod(typeof(MapDebugLog), nameof(Postfix)));
                Log.Message("[HSKMoreHardcore] MapDebugLog applied.");
            }
        }

        public static void Postfix(Map __instance)
        {
            var map = __instance;
            var parent = map.Parent;
            var site = parent as Site;

            string parts = "none";
            if (site != null && site.parts != null)
            {
                parts = string.Join(", ", site.parts.Select(p =>
                    $"{p.def.defName}(tags:{(p.def.tags != null ? string.Join(";", p.def.tags) : "null")}, worker:{p.def.workerClass?.Name})"));
            }

            Log.Warning($"[MapDebugLog] Map loaded: IsPlayerHome={map.IsPlayerHome}, " +
                $"ParentType={parent?.GetType()?.Name}, ParentDef={parent?.def?.defName}, " +
                $"IsSite={site != null}, SiteParts=[{parts}], " +
                $"Tile={map.Tile}, MapId={map.uniqueID}");
        }
    }
}
