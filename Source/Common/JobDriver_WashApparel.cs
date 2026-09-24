using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace HSKMoreHardcore
{
    // Несёт помеченную одежду к тазику, отрабатывает время и стирает на месте
    // (снимает «Ношеное»/«Со следами боя», ставит «Постирано»), не уничтожая вещь.
    public class JobDriver_WashApparel : JobDriver
    {
        private float workLeft;

        private Building Bench => job.GetTarget(TargetIndex.A).Thing as Building;
        private Apparel TargetApparel => job.GetTarget(TargetIndex.B).Thing as Apparel;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed)
                && pawn.Reserve(job.GetTarget(TargetIndex.B), job, 1, -1, null, errorOnFailed);
        }

        private float TotalWork => job.bill?.recipe != null ? job.bill.recipe.WorkAmountTotal(null) : 600f;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(TargetIndex.B);
            this.FailOnDestroyedNullOrForbidden(TargetIndex.A);
            this.FailOnBurningImmobile(TargetIndex.A);

            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(TargetIndex.B);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            Toil work = new Toil();
            work.initAction = delegate
            {
                workLeft = TotalWork;
                job.bill?.Notify_DoBillStarted(pawn);
            };
            work.tickAction = delegate
            {
                float speed = (job.bill?.recipe?.workSpeedStat != null)
                    ? pawn.GetStatValue(job.bill.recipe.workSpeedStat)
                    : 1f;
                workLeft -= speed;
                (Bench as Building_WorkTable)?.UsedThisTick();

                if (workLeft <= 0f)
                {
                    TargetApparel?.TryGetComp<CompWornByEnemy>()?.Wash();
                    job.bill?.Notify_IterationCompleted(pawn, new List<Thing> { TargetApparel });
                    ReadyForNextToil();
                }
            };
            work.defaultCompleteMode = ToilCompleteMode.Never;
            work.WithProgressBar(TargetIndex.A, () => 1f - workLeft / TotalWork);
            work.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            work.FailOn(() => job.bill == null || job.bill.suspended || job.bill.DeletedOrDereferenced);
            if (job.bill?.recipe?.workSkill != null)
                work.activeSkill = () => job.bill.recipe.workSkill;
            yield return work;

            Toil drop = new Toil();
            drop.initAction = delegate
            {
                if (pawn.carryTracker.CarriedThing != null)
                    pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
            };
            yield return drop;
        }
    }
}
