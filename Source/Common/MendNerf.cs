using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    // Завязка на починку Mend & Recycle:
    //  - после починки вешаем метку «Починено» и +1 к счётчику (repairCount);
    //  - при repairCount >= repairBlockThreshold обычная починка запрещена.
    // Точки чистые (именованные методы): через XML нельзя — рецепты починки генерятся кодом.
    [StaticConstructorOnStartup]
    public static class MendNerf
    {
        private static JobDef mendJob;

        static MendNerf()
        {
            mendJob = DefDatabase<JobDef>.GetNamedSilentFail("Mend");

            var harmony = new Harmony("linya.hskmorehardcore.mendnerf");

            // Блок обычной починки: EnoughSkill — обязательное условие предиката поиска починки
            // (recycle идёт через ignoreHitPoints, минуя его, поэтому разборку не задеваем).
            var enoughSkill = AccessTools.Method("Mending.WorkGiver_DoBill:EnoughSkill");
            if (enoughSkill != null)
            {
                harmony.Patch(enoughSkill,
                    postfix: new HarmonyMethod(typeof(MendNerf), nameof(EnoughSkillPostfix)));
                Log.Message("[HSKMoreHardcore] MendNerf (repair block) applied.");
            }
            else
            {
                Log.Warning("[HSKMoreHardcore] MendNerf: Mending.WorkGiver_DoBill.EnoughSkill not found (Mend&Recycle missing?).");
            }

            // Счётчик + метка после починки: гейтим по джобу Mend.
            // Патчим Bill_Production (он ПЕРЕОПРЕДЕЛЯЕТ Notify_IterationCompleted — у мендовых билов
            // именно этот тип, патч базового Bill сюда не доходит).
            var notify = AccessTools.Method(typeof(Bill_Production), nameof(Bill.Notify_IterationCompleted));
            if (notify != null && mendJob != null)
            {
                harmony.Patch(notify,
                    postfix: new HarmonyMethod(typeof(MendNerf), nameof(NotifyIterationCompletedPostfix)));
                Log.Message("[HSKMoreHardcore] MendNerf (repair counter) applied.");
            }
        }

        // EnoughSkill(Bill bill, Thing thing, float k, ref float skillNeeded, ref Thing repairable)
        public static void EnoughSkillPostfix(Thing thing, ref bool __result)
        {
            if (!__result)
                return;
            var comp = thing?.TryGetComp<CompWornByEnemy>();
            if (comp != null && !comp.CanMendNormally)
            {
                __result = false;
            }
        }

        public static void NotifyIterationCompletedPostfix(Pawn billDoer, List<Thing> ingredients)
        {
            var curDef = billDoer?.CurJob?.def;
            if (curDef != mendJob)
                return;
            if (ingredients == null)
                return;
            foreach (var t in ingredients)
            {
                if (t == null || t.Destroyed)
                    continue;
                var comp = t.TryGetComp<CompWornByEnemy>();
                if (comp == null)
                    continue;
                comp.repairCount++;
            }
        }
    }
}
