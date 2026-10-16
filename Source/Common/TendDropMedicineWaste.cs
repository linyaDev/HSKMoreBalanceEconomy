using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HSKMoreHardcore
{
    // Когда доктор берётся лечить, медицина кладётся ему в руки (carryTracker).
    // Если джоб TendPatient прерывается (пациент встал/умер, отвлекли, дали другой приказ),
    // движок в Pawn_JobTracker.EndCurrentJob -> CleanupCurrentJob роняет медицину на землю.
    // Ловим момент в префиксе EndCurrentJob (он идёт ДО выпадения) и, если завершение
    // НЕ успешное, уничтожаем зажатую в руках медицину — прерванное лечение тратит медкит.
    // Чистой точки нет, рантайм — поэтому XML не подходит, только Harmony.
    [StaticConstructorOnStartup]
    public static class TendDropMedicineWaste
    {
        private static readonly AccessTools.FieldRef<Pawn_JobTracker, Pawn> pawnField =
            AccessTools.FieldRefAccess<Pawn_JobTracker, Pawn>("pawn");

        static TendDropMedicineWaste()
        {
            var endCurrentJob = AccessTools.Method(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.EndCurrentJob));
            if (endCurrentJob == null)
            {
                Log.Warning("[HSKMoreHardcore] TendDropMedicineWaste: Pawn_JobTracker.EndCurrentJob not found.");
                return;
            }

            var harmony = new Harmony("linya.hskmorehardcore.tenddropmedicine");
            harmony.Patch(endCurrentJob,
                prefix: new HarmonyMethod(typeof(TendDropMedicineWaste), nameof(Prefix)));
            Log.Message("[HSKMoreHardcore] TendDropMedicineWaste applied.");
        }

        public static void Prefix(Pawn_JobTracker __instance, JobCondition condition)
        {
            // Успешное лечение не трогаем — остаток медицины штатно вернётся на склад.
            if (condition == JobCondition.Succeeded)
                return;

            var job = __instance.curJob;
            if (job == null || job.def != JobDefOf.TendPatient)
                return;

            var pawn = pawnField(__instance);
            if (pawn == null || pawn.Faction == Faction.OfPlayer)
                return;

            var carried = pawn.carryTracker?.CarriedThing;
            if (carried == null || !carried.def.IsMedicine)
                return;

            // Убираем из рук и уничтожаем, пока CleanupCurrentJob не уронил на пол.
            pawn.carryTracker.innerContainer.Remove(carried);
            carried.Destroy(DestroyMode.Vanish);
        }
    }
}
