using RimWorld;
using Verse;
using Verse.AI;

namespace HSKMoreHardcore
{
    // Свой не-уничтожающий пайплайн стирки (строго раздельно с трупной стиркой HolyWash).
    // Обрабатывает билы нашего рецепта HSK_WashApparel на HolyBasin и выдаёт нашу джобу.
    public class WorkGiver_WashApparel : WorkGiver_Scanner
    {
        public const string RecipeDefName = "HSK_WashApparel";

        private static JobDef washJobDef;
        private static JobDef WashJobDef => washJobDef ??= DefDatabase<JobDef>.GetNamed("HSK_DoWashApparel");

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
                if (bill.recipe == null || bill.recipe.defName != RecipeDefName)
                    continue;
                if (!bill.ShouldDoNow() || !bill.PawnAllowedToStartAnew(pawn))
                    continue;
                if (!bill.recipe.PawnSatisfiesSkillRequirements(pawn))
                {
                    JobFailReason.Is("MissingSkill".Translate());
                    continue;
                }

                Thing apparel = FindApparel(pawn, bill, thing);
                if (apparel == null)
                    continue;

                Job job = JobMaker.MakeJob(WashJobDef, thing, apparel);
                job.bill = bill;
                job.count = 1;
                return job;
            }
            return null;
        }

        private static Thing FindApparel(Pawn pawn, Bill bill, Thing bench)
        {
            Thing best = null;
            float bestDistSq = float.MaxValue;
            float radSq = bill.ingredientSearchRadius * bill.ingredientSearchRadius;

            foreach (Thing t in pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel))
            {
                if (t.IsForbidden(pawn) || !bill.IsFixedOrAllowedIngredient(t))
                    continue;
                float distSq = (t.Position - bench.Position).LengthHorizontalSquared;
                if (distSq > radSq || distSq >= bestDistSq)
                    continue;
                if (!pawn.CanReserve(t) || !pawn.CanReach(t, PathEndMode.ClosestTouch, Danger.Some))
                    continue;
                best = t;
                bestDistSq = distSq;
            }
            return best;
        }
    }
}
