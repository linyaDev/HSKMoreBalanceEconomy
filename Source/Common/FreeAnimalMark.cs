using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    // Помечает животных, которые достались даром по событию: пришедшее стадо,
    // боевые и караванные животные, самоприручившийся зверь. Метка (CompFreeAnimal)
    // режет цену продажи — множитель freeAnimalSellMultiplier в HardcoreSettings.xml.
    //
    // Все четыре воркера в конце делают pawn.SetFaction(Faction.OfPlayer), поэтому
    // хук один: на время TryExecuteWorker поднимаем флаг, а постфикс SetFaction
    // метит любое животное, перешедшее игроку, пока флаг поднят. Ручное приручение,
    // покупка и рождение флага не поднимают и метку не получают.
    [StaticConstructorOnStartup]
    public static class FreeAnimalMark
    {
        private static readonly string[] workerTypeNames =
        {
            "RimWorld.IncidentWorker_FarmAnimalsWanderIn",
            "RimWorld.IncidentWorker_SelfTame",
            "SK.Events.IncidentWorker_AnimalBattle",
            "SK.Events.IncidentWorker_CaravanAnimal",
        };

        private static bool inFreeAnimalIncident;

        static FreeAnimalMark()
        {
            var harmony = new Harmony("linya.hskmorehardcore.freeanimalmark");

            int patched = 0;
            foreach (var typeName in workerTypeNames)
            {
                var type = AccessTools.TypeByName(typeName);
                var method = type == null ? null : AccessTools.DeclaredMethod(type, "TryExecuteWorker");
                if (method == null)
                    continue;

                harmony.Patch(method,
                    prefix: new HarmonyMethod(typeof(FreeAnimalMark), nameof(WorkerPrefix)),
                    finalizer: new HarmonyMethod(typeof(FreeAnimalMark), nameof(WorkerFinalizer)));
                patched++;
            }

            if (patched == 0)
            {
                Log.Warning("[HSKMoreHardcore] FreeAnimalMark: ни один воркер событий с животными не найден.");
                return;
            }

            var setFaction = AccessTools.Method(typeof(Pawn), "SetFaction");
            if (setFaction == null)
            {
                Log.Warning("[HSKMoreHardcore] FreeAnimalMark: Pawn.SetFaction не найден — животные не метятся.");
                return;
            }

            harmony.Patch(setFaction,
                postfix: new HarmonyMethod(typeof(FreeAnimalMark), nameof(SetFactionPostfix)));

            // Ручное приручение: постфикс на общий метод вербовки/приручения.
            // Через SetFaction это не поймать — туда же попадают новорождённые
            // (PawnGenerator ставит фракцию матери вызовом SetFaction).
            var doRecruit = AccessTools.Method(typeof(InteractionWorker_RecruitAttempt), "DoRecruit",
                new[] { typeof(Pawn), typeof(Pawn), typeof(string).MakeByRefType(), typeof(string).MakeByRefType(), typeof(bool), typeof(bool) });
            if (doRecruit != null)
                harmony.Patch(doRecruit,
                    postfix: new HarmonyMethod(typeof(FreeAnimalMark), nameof(DoRecruitPostfix)));
            else
                Log.Warning("[HSKMoreHardcore] FreeAnimalMark: InteractionWorker_RecruitAttempt.DoRecruit не найден — прирученные животные не метятся.");
        }

        public static void WorkerPrefix()
        {
            inFreeAnimalIncident = true;
        }

        // Finalizer, а не постфикс: флаг снимется даже если воркер бросит исключение
        public static void WorkerFinalizer()
        {
            inFreeAnimalIncident = false;
        }

        public static void SetFactionPostfix(Pawn __instance, Faction newFaction)
        {
            if (!inFreeAnimalIncident || __instance == null || newFaction == null)
                return;
            if (!newFaction.IsPlayer || __instance.RaceProps == null || !__instance.RaceProps.Animal)
                return;

            var comp = __instance.TryGetComp<CompFreeAnimal>();
            if (comp != null)
                comp.joinedFree = true;
        }

        public static void DoRecruitPostfix(Pawn recruitee)
        {
            if (recruitee?.RaceProps == null || !recruitee.RaceProps.Animal)
                return;
            if (recruitee.Faction == null || !recruitee.Faction.IsPlayer)
                return;

            var comp = recruitee.TryGetComp<CompFreeAnimal>();
            if (comp != null && !comp.joinedFree)
                comp.tamed = true;
        }
    }
}
