using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HSKMoreHardcore
{
    /// <summary>
    /// Переделка починки Mend & Recycle: вместо потолка от навыка (progressive
    /// mending: 5% x (навык+5) от макс. HP) каждая починка восстанавливает до
    /// +50% макс. прочности от стартовой, независимо от навыка. Навык влияет
    /// только на шанс порчи при ремонте (родная механика мода) и опыт.
    /// Вместе с MendNerf (блок при 2 метках «Починено») выходит: за жизнь вещь
    /// чинится максимум на один полный запас прочности.
    /// Реализация — подмена тойла JobDriver_Mend.DoBill (логика скопирована из
    /// мода, изменён только расчёт цели ремонта) и обход скилл-гейта в
    /// WorkGiver_DoBill.EnoughSkill (блок по счётчику остаётся за MendNerf).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class MendFlatRepair
    {
        // Доля макс. прочности, восстанавливаемая за одну починку
        public const float RepairFractionPerJob = 0.5f;

        // Родные константы Mend & Recycle
        private const int HitPointsPerCycle = 5;
        private const int FailDamage = 50;

        private static readonly MethodInfo readyForNextToil =
            AccessTools.Method(typeof(JobDriver), "ReadyForNextToil");

        private static MethodInfo chanceGetFor;   // Mending.ChanceDef.GetFor(Thing) : ChanceDef
        private static MethodInfo chanceChance;   // Mending.ChanceDef.Chance(QualityCategory) : float
        private static MethodInfo reclaim;        // Mending.JobDriverUtils.Reclaim(Thing, float) : List<Thing>
        private static FieldInfo allowToRemoveDebuffField; // Mending.Mending_Settings.allowToRemoveDebuff
        private static FieldInfo decreaseQualityField;     // Mending.Mending_Settings.decreaseQualityOnFail

        static MendFlatRepair()
        {
            var driver = AccessTools.TypeByName("Mending.JobDriver_Mend");
            var workGiver = AccessTools.TypeByName("Mending.WorkGiver_DoBill");
            if (driver == null || workGiver == null)
            {
                Log.Warning("[HSKMoreHardcore] MendFlatRepair: Mend&Recycle не найден — починка не переделана.");
                return;
            }

            var doBill = AccessTools.Method(driver, "DoBill");
            var enoughSkill = AccessTools.Method(workGiver, "EnoughSkill");
            if (doBill == null || enoughSkill == null)
            {
                Log.Warning("[HSKMoreHardcore] MendFlatRepair: API Mend&Recycle изменилось — починка не переделана.");
                return;
            }

            chanceGetFor = AccessTools.Method("Mending.ChanceDef:GetFor");
            chanceChance = AccessTools.Method("Mending.ChanceDef:Chance");
            reclaim = AccessTools.Method("Mending.JobDriverUtils:Reclaim");
            allowToRemoveDebuffField = AccessTools.Field("Mending.Mending_Settings:allowToRemoveDebuff");
            decreaseQualityField = AccessTools.Field("Mending.Mending_Settings:decreaseQualityOnFail");

            var harmony = new Harmony("linya.hskmorehardcore.mendflatrepair");
            harmony.Patch(doBill, prefix: new HarmonyMethod(typeof(MendFlatRepair), nameof(DoBillPrefix)));
            harmony.Patch(enoughSkill, prefix: new HarmonyMethod(typeof(MendFlatRepair), nameof(EnoughSkillPrefix)));
            Log.Message("[HSKMoreHardcore] MendFlatRepair applied: починка +50% прочности за раз, без потолка по навыку.");
        }

        // Навык больше не ограничивает, кого пускать чинить. Постфикс MendNerf
        // на этом же методе (блок по счётчику «Починено») продолжает работать.
        public static bool EnoughSkillPrefix(ref bool __result)
        {
            __result = true;
            return false;
        }

        public static bool DoBillPrefix(JobDriver __instance, ref Toil __result)
        {
            __result = BuildMendToil(__instance);
            return false;
        }

        // Копия Mending.JobDriver_Mend.DoBill с одним отличием: цель ремонта —
        // стартовая прочность + RepairFractionPerJob (потолок 100%), а не
        // доля от навыка пешки.
        private static Toil BuildMendToil(JobDriver driver)
        {
            Pawn pawn = driver.pawn;
            Job job = driver.job;
            Thing objectThing = job.GetTarget(TargetIndex.B).Thing;
            Building_WorkTable tableThing = job.GetTarget(TargetIndex.A).Thing as Building_WorkTable;

            float targetFraction = 1f;
            object failChance = null;
            float workCycle = 0f;
            int workCycleProgress = 0;

            Toil toil = new Toil();
            toil.initAction = delegate
            {
                job.bill.Notify_DoBillStarted(pawn);
                failChance = chanceGetFor != null ? chanceGetFor.Invoke(null, new object[] { objectThing }) : null;
                workCycle = Math.Max(job.bill.recipe.workAmount, 10f);
                workCycleProgress = (int)workCycle;
                if (objectThing != null)
                    targetFraction = Mathf.Min(
                        (float)objectThing.HitPoints / objectThing.MaxHitPoints + RepairFractionPerJob, 1f);
            };
            toil.tickAction = delegate
            {
                if (objectThing == null || objectThing.Destroyed)
                {
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                }
                workCycleProgress--;
                if (tableThing == null)
                {
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }
                tableThing.UsedThisTick();
                if (!tableThing.UsableForBillsAfterFueling())
                {
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                }
                if (workCycleProgress > 0 || objectThing == null)
                    return;

                int targetHp = (int)(objectThing.MaxHitPoints * targetFraction);
                int missing = targetHp - objectThing.HitPoints;
                if (missing > 0)
                    objectThing.HitPoints += Math.Min(missing, HitPointsPerCycle);

                float t = 0.5f;
                SkillDef workSkill = job.RecipeDef.workSkill;
                if (workSkill != null)
                {
                    SkillRecord skill = pawn.skills?.GetSkill(workSkill);
                    t = (float)(skill?.GetLevel() ?? pawn.RaceProps.mechFixedSkillLevel) / 20f;
                    skill?.Learn(Math.Min(missing, HitPointsPerCycle) * job.RecipeDef.workSkillLearnFactor);
                }

                // Шанс порчи — родной, по качеству вещи и навыку пешки
                CompQuality compQuality = objectThing.TryGetComp<CompQuality>();
                if (compQuality != null && (int)compQuality.Quality > 0 && failChance != null && chanceChance != null)
                {
                    QualityCategory quality = compQuality.Quality;
                    float failFactor = Mathf.Lerp(1.5f, 0f, t);
                    float chance = (float)chanceChance.Invoke(failChance, new object[] { quality });
                    if (Rand.Value < chance * failFactor)
                    {
                        objectThing.HitPoints -= FailDamage;
                        bool decreaseQuality = Mending_DecreaseQualityOnFail();
                        if (decreaseQuality && (int)quality > 0)
                            compQuality.SetQuality(compQuality.Quality - 1, ArtGenerationContext.Colony);
                        MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "Failed");
                    }
                }

                pawn.GainComfortFromCellIfPossible(1);

                if (objectThing.HitPoints <= 0)
                {
                    // Запороли насмерть: разобрать на материалы, как в моде
                    float reclaimFactor = Mathf.Lerp(0.5f, 1.5f, t);
                    var list = reclaim != null
                        ? (List<Thing>)reclaim.Invoke(null, new object[] { objectThing, reclaimFactor * 0.1f })
                        : new List<Thing>();
                    pawn.Map.reservationManager.Release(job.targetB, pawn, job);
                    objectThing.Destroy();
                    if (list.Count == 0)
                    {
                        pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                        return;
                    }
                    for (int i = 1; i < list.Count; i++)
                    {
                        if (!GenPlace.TryPlaceThing(list[i], pawn.Position, pawn.Map, ThingPlaceMode.Near))
                            Log.Error($"[HSKMoreHardcore] MendFlatRepair: не удалось выложить {list[i]} возле {pawn.Position}");
                    }
                    list[0].SetPositionDirect(pawn.Position);
                    job.targetB = list[0];
                    job.bill.Notify_IterationCompleted(pawn, list);
                    pawn.Map.reservationManager.Reserve(pawn, job, job.targetB);
                    readyForNextToil.Invoke(driver, null);
                }
                else if (objectThing.HitPoints >= targetHp || objectThing.HitPoints == objectThing.MaxHitPoints)
                {
                    if (objectThing is Apparel apparel && Mending_AllowToRemoveDebuff())
                        apparel.WornByCorpse = false;
                    job.bill.Notify_IterationCompleted(pawn, new List<Thing> { objectThing });
                    readyForNextToil.Invoke(driver, null);
                }

                workCycleProgress = (int)workCycle;
            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.WithEffect(() => job.bill.recipe.effectWorking, TargetIndex.A);
            toil.PlaySustainerOrSound(() => toil.actor.CurJob.bill.recipe.soundWorking);
            toil.WithProgressBar(TargetIndex.A,
                () => objectThing.HitPoints / (objectThing.MaxHitPoints * targetFraction),
                interpolateBetweenActorAndTarget: false, 0.5f);
            toil.FailOn((Func<bool>)delegate
            {
                IBillGiver billGiver = job.GetTarget(TargetIndex.A).Thing as IBillGiver;
                return job.bill.suspended || job.bill.DeletedOrDereferenced
                    || (billGiver != null && !billGiver.CurrentlyUsableForBills());
            });
            return toil;
        }

        private static bool Mending_DecreaseQualityOnFail()
        {
            return decreaseQualityField == null || (bool)decreaseQualityField.GetValue(null);
        }

        private static bool Mending_AllowToRemoveDebuff()
        {
            return allowToRemoveDebuffField != null && (bool)allowToRemoveDebuffField.GetValue(null);
        }
    }
}
