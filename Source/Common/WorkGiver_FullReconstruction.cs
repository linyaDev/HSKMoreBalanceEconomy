using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HSKMoreHardcore
{
    // Свой потребляющий пайплайн «полной реконструкции» на TableMending.
    // Чинит повреждённую вещь из стаффа до 100%, расходуя 75% базового стаффа,
    // и сбрасывает счётчик «Починено». Свой WorkGiver, т.к. цена считается от вещи динамически.
    public class WorkGiver_FullReconstruction : WorkGiver_Scanner
    {
        public const string RecipeDefName = "HSK_FullReconstruction";
        public const float StuffFraction = 0.75f;

        private static JobDef jobDef;
        private static JobDef JobDefCached => jobDef ??= DefDatabase<JobDef>.GetNamed("HSK_DoFullReconstruction");

        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.PotentialBillGiver);
        public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            if (!(thing is IBillGiver giver))
                return null;
            if (def.fixedBillGiverDefs == null || !def.fixedBillGiverDefs.Contains(thing.def))
                return null;
            if (!giver.CurrentlyUsableForBills() || !giver.BillStack.AnyShouldDoNow)
                return null;
            if (!pawn.CanReserve(thing, 1, -1, null, forced) || thing.IsBurning() || thing.IsForbidden(pawn))
                return null;
            if (!pawn.CanReach(thing.InteractionCell, PathEndMode.OnCell, Danger.Some))
                return null;

            giver.BillStack.RemoveIncompletableBills();

            for (int i = 0; i < giver.BillStack.Count; i++)
            {
                Bill bill = giver.BillStack[i];
                if (bill.recipe == null || !bill.recipe.defName.StartsWith(RecipeDefName))
                    continue;
                if (!bill.ShouldDoNow() || !bill.PawnAllowedToStartAnew(pawn))
                    continue;
                if (!bill.recipe.PawnSatisfiesSkillRequirements(pawn))
                {
                    JobFailReason.Is("MissingSkill".Translate());
                    continue;
                }

                Thing item = FindItem(pawn, bill, thing);
                if (item == null)
                    continue;

                int need = NeededStuff(item);
                var stuffStacks = new List<Thing>();
                var counts = new List<int>();
                if (!FindStuff(pawn, bill, thing, item.Stuff, need, stuffStacks, counts))
                {
                    JobFailReason.Is("HSKMoreHardcore_NoReconstructMaterial".Translate(need, item.Stuff.label));
                    continue;
                }

                Job job = JobMaker.MakeJob(JobDefCached, thing); // A = верстак
                job.bill = bill;
                job.targetC = item;                              // C = вещь
                job.targetQueueB = new List<LocalTargetInfo>();  // B = очередь стаффа (подносим по одному)
                foreach (var s in stuffStacks)
                    job.targetQueueB.Add(s);
                job.countQueue = counts;
                job.count = 1;
                return job;
            }
            return null;
        }

        // Нужное количество стаффа: 75% от базовой стоимости крафта (минимум 1).
        public static int NeededStuff(Thing item)
        {
            return Mathf.Max(1, Mathf.CeilToInt(item.def.costStuffCount * StuffFraction));
        }

        // Ближайшая повреждённая вещь из стаффа (одежда или оружие), подходящая под бил.
        private static Thing FindItem(Pawn pawn, Bill bill, Thing bench)
        {
            Thing best = null;
            float bestDistSq = float.MaxValue;
            float radSq = bill.ingredientSearchRadius * bill.ingredientSearchRadius;

            ScanGroup(pawn, bill, bench, ThingRequestGroup.Apparel, radSq, ref best, ref bestDistSq);
            ScanGroup(pawn, bill, bench, ThingRequestGroup.Weapon, radSq, ref best, ref bestDistSq);
            return best;
        }

        private static void ScanGroup(Pawn pawn, Bill bill, Thing bench, ThingRequestGroup group,
            float radSq, ref Thing best, ref float bestDistSq)
        {
            foreach (Thing t in pawn.Map.listerThings.ThingsInGroup(group))
            {
                if (t.IsForbidden(pawn))
                    continue;
                if (t.def == null || !t.def.MadeFromStuff || t.Stuff == null || t.def.costStuffCount <= 0)
                    continue;
                if (t.HitPoints >= t.MaxHitPoints) // только повреждённые
                    continue;
                // Только вещи, отремонтированные обычной починкой >= порога (когда обычная уже заблокирована)
                var comp = t.TryGetComp<CompWornByEnemy>();
                if (comp == null || comp.repairCount < NerfSettings.repairBlockThreshold)
                    continue;
                if (!bill.IsFixedOrAllowedIngredient(t))
                    continue;
                float distSq = (t.Position - bench.Position).LengthHorizontalSquared;
                if (distSq > radSq || distSq >= bestDistSq)
                    continue;
                if (!pawn.CanReserve(t) || !pawn.CanReach(t, PathEndMode.ClosestTouch, Danger.Some))
                    continue;
                best = t;
                bestDistSq = distSq;
            }
        }

        // Набирает стеки стаффа нужного дефа в радиусе бил-а, пока не наберём need.
        private static bool FindStuff(Pawn pawn, Bill bill, Thing bench, ThingDef stuffDef, int need,
            List<Thing> outStacks, List<int> outCounts)
        {
            if (stuffDef == null)
                return false;
            float radSq = bill.ingredientSearchRadius * bill.ingredientSearchRadius;
            int remaining = need;
            foreach (Thing t in pawn.Map.listerThings.ThingsOfDef(stuffDef))
            {
                if (remaining <= 0)
                    break;
                if (t.IsForbidden(pawn) || t.stackCount <= 0)
                    continue;
                float distSq = (t.Position - bench.Position).LengthHorizontalSquared;
                if (distSq > radSq)
                    continue;
                if (!pawn.CanReserve(t) || !pawn.CanReach(t, PathEndMode.ClosestTouch, Danger.Some))
                    continue;
                int take = Mathf.Min(remaining, t.stackCount);
                outStacks.Add(t);
                outCounts.Add(take);
                remaining -= take;
            }
            return remaining <= 0;
        }
    }
}
