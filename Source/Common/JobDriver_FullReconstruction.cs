using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace HSKMoreHardcore
{
    // Пешка подносит к верстаку стафф (75% базового, по очереди), списывает его при доставке,
    // затем несёт повреждённую вещь, отрабатывает время, чинит до 100% и сбрасывает «Починено».
    // Раскладка целей: A = верстак, C = вещь, очередь B = стеки стаффа.
    public class JobDriver_FullReconstruction : JobDriver
    {
        private float workLeft;
        private int consumedStuff;

        private Building Bench => job.GetTarget(TargetIndex.A).Thing as Building;
        private Thing TargetItem => job.GetTarget(TargetIndex.C).Thing;

        private float TotalWork => job.bill?.recipe != null ? job.bill.recipe.WorkAmountTotal(null) : 2500f;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed))
                return false;
            if (!pawn.Reserve(job.GetTarget(TargetIndex.C), job, 1, -1, null, errorOnFailed))
                return false;
            if (job.targetQueueB != null)
            {
                for (int i = 0; i < job.targetQueueB.Count; i++)
                {
                    int cnt = (job.countQueue != null && i < job.countQueue.Count) ? job.countQueue[i] : 1;
                    if (!pawn.Reserve(job.targetQueueB[i], job, 1, cnt, null, errorOnFailed))
                        return false;
                }
            }
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(TargetIndex.C);
            this.FailOnDestroyedNullOrForbidden(TargetIndex.A);
            this.FailOnBurningImmobile(TargetIndex.A);

            // 1) Подносим стафф к верстаку по очереди и списываем при доставке.
            Toil extract = Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.B);
            yield return extract;
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(TargetIndex.B);
            // Количество на каждый стек ставит ExtractNextTargetFromQueue из countQueue,
            // вычитать взятое не нужно — иначе job.count обнуляется и портит перенос вещи
            yield return Toils_Haul.StartCarryThing(TargetIndex.B);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            Toil consume = new Toil();
            consume.initAction = delegate
            {
                Thing carried = pawn.carryTracker.CarriedThing;
                if (carried != null)
                {
                    consumedStuff += carried.stackCount;
                    pawn.carryTracker.innerContainer.Remove(carried);
                    carried.Destroy();
                }
            };
            consume.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return consume;
            yield return Toils_Jump.JumpIfHaveTargetInQueue(TargetIndex.B, extract);

            // 2) Несём вещь к верстаку. Счётчик задаём заново: в нём осталось
            // количество последнего стека стаффа, а вещь несём ровно одну.
            Toil resetCount = new Toil();
            resetCount.initAction = delegate { job.count = 1; };
            resetCount.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return resetCount;

            yield return Toils_Goto.GotoThing(TargetIndex.C, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(TargetIndex.C);
            yield return Toils_Haul.StartCarryThing(TargetIndex.C);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            // 3) Работа -> полный ремонт.
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
                    CompleteReconstruction();
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

            // 4) Бросаем вещь.
            Toil drop = new Toil();
            drop.initAction = delegate
            {
                if (pawn.carryTracker.CarriedThing != null)
                    pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
            };
            yield return drop;
        }

        private void CompleteReconstruction()
        {
            Thing item = TargetItem ?? pawn.carryTracker.CarriedThing;
            if (item == null)
                return;

            item.HitPoints = item.MaxHitPoints;
            var comp = item.TryGetComp<CompWornByEnemy>();
            if (comp != null)
            {
                comp.repairCount = 0; // снимаем «Починено»
            }

            job.bill?.Notify_IterationCompleted(pawn, new List<Thing> { item });
        }
    }
}
