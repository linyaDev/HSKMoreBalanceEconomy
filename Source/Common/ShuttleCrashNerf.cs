using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace HSKMoreHardcore
{
    [StaticConstructorOnStartup]
    public static class ShuttleCrashNerf
    {
        private static System.Type ammoThingType;

        static ShuttleCrashNerf()
        {
            ammoThingType = AccessTools.TypeByName("CombatExtended.AmmoThing");

            var harmony = new Harmony("linya.hskmorehardcore.shuttlecrashnerf");
            var target = AccessTools.Method(typeof(QuestNode_Root_ShuttleCrash_Rescue), "RunInt");
            if (target != null)
            {
                harmony.Patch(target,
                    postfix: new HarmonyMethod(typeof(ShuttleCrashNerf), nameof(Postfix)));
                Log.Message("[HSKMoreHardcore] ShuttleCrashNerf applied.");
            }
        }

        public static void Postfix()
        {
            var slate = QuestGen.slate;
            if (slate == null)
                return;

            var soldiers = slate.Get<List<Pawn>>("soldiers");
            var civilians = slate.Get<List<Pawn>>("civilians");

            if (soldiers != null)
            {
                foreach (var pawn in soldiers)
                    NerfPawn(pawn);
            }

            if (civilians != null)
            {
                foreach (var pawn in civilians)
                    NerfPawn(pawn);
            }
        }

        private static void NerfPawn(Pawn pawn)
        {
            if (pawn == null)
                return;

            // Снять шлемы
            if (pawn.apparel != null)
            {
                var toRemove = new List<Apparel>();
                foreach (var ap in pawn.apparel.WornApparel)
                {
                    if (ap.def.apparel?.bodyPartGroups != null)
                    {
                        foreach (var bpg in ap.def.apparel.bodyPartGroups)
                        {
                            if (bpg.defName == "FullHead" || bpg.defName == "UpperHead")
                            {
                                toRemove.Add(ap);
                                break;
                            }
                        }
                    }
                }

                foreach (var ap in toRemove)
                {
                    pawn.apparel.Remove(ap);
                    ap.Destroy();
                }
            }

            // Урезать патроны в инвентаре
            if (pawn.inventory?.innerContainer != null)
            {
                var toReduce = new List<Thing>();
                foreach (var thing in pawn.inventory.innerContainer)
                {
                    if (ammoThingType != null && ammoThingType.IsInstanceOfType(thing))
                        toReduce.Add(thing);
                }

                foreach (var thing in toReduce)
                {
                    thing.stackCount = UnityEngine.Mathf.Min(5, thing.stackCount);
                }
            }
        }
    }
}
