using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HSKMoreHardcore
{
    /// <summary>
    /// Когда безумное животное получает урон от колониста, все manhunter в радиусе
    /// 40 клеток от раненого получают хедифф HSK_ManhunterRage (жажда мести) на 3 часа.
    /// Только животные с этим хедиффом ломают двери — и только если их цель пешка игрока.
    /// Приоритет у досягаемых целей: пока есть кто-то, до кого можно дойти без
    /// ломания дверей, зверь дерётся с ним и двери не трогает.
    /// За чужими пешками (рейдеры, гости) гоняются как в ванилле.
    /// Как только manhunter кого-то опрокинул или убил, его хедифф снимается (ярость остаётся).
    /// Хедифф сохраняется в сейве и виден во вкладке здоровья животного;
    /// над животным рисуется пульсирующий «!» с тултипом.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ManhunterDoorBreak
    {
        private const bool Enabled = true;

        // Диагностика: урон/убийства/решения по каждому людоеду в лог.
        // Причина пропуска пишется только при смене её категории для животного.
        private const bool DebugLog = true;

        // Длительность жажды мести: 3 часа (7500 тиков) после урона
        private const int AggroWindowTicks = 7500;

        // Радиус заражения яростью от раненого животного
        private const float RageRadius = 40f;

        // Последняя записанная в лог категория решения по животному (thingIDNumber -> категория)
        private static readonly Dictionary<int, string> lastLoggedDecision = new Dictionary<int, string>();

        private static HediffDef rageDef;
        private static HediffDef RageDef =>
            rageDef ??= DefDatabase<HediffDef>.GetNamed("HSK_ManhunterRage");

        // Иконка «!» над животным с жаждой мести (грузится в статическом
        // конструкторе — он выполняется в главном потоке)
        private static readonly Texture2D RageIcon =
            ContentFinder<Texture2D>.Get("HMH/ManhunterRageMark", reportFailure: false);

        static ManhunterDoorBreak()
        {
            if (!Enabled)
            {
                Log.Message("[HSKMoreHardcore] ManhunterDoorBreak disabled.");
                return;
            }

            var harmony = new Harmony("linya.hskmorehardcore.manhunterdoorbreak");

            // Postfix на TryGiveJob — перенаправить на двери
            var tryGiveJob = AccessTools.Method(typeof(JobGiver_Manhunter), "TryGiveJob");
            if (tryGiveJob != null)
            {
                harmony.Patch(tryGiveJob,
                    postfix: new HarmonyMethod(typeof(ManhunterDoorBreak), nameof(JobPostfix)));
            }

            // Postfix на Pawn_MindState.Notify_DamageTaken — отследить урон по manhunter
            var notifyDamage = AccessTools.Method(typeof(Pawn_MindState), "Notify_DamageTaken");
            if (notifyDamage != null)
            {
                harmony.Patch(notifyDamage,
                    postfix: new HarmonyMethod(typeof(ManhunterDoorBreak), nameof(DamagePostfix)));
            }

            // Postfix на Pawn.Kill — manhunter кого-то убил: снять его жажду мести
            var kill = AccessTools.Method(typeof(Pawn), nameof(Pawn.Kill));
            if (kill != null)
                harmony.Patch(kill, postfix: new HarmonyMethod(typeof(ManhunterDoorBreak), nameof(KillPostfix)));

            // Postfix на Pawn_HealthTracker.MakeDowned — manhunter кого-то опрокинул: снять хедифф
            var makeDowned = AccessTools.Method(typeof(Pawn_HealthTracker), "MakeDowned");
            if (makeDowned != null)
                harmony.Patch(makeDowned, postfix: new HarmonyMethod(typeof(ManhunterDoorBreak), nameof(DownedPostfix)));
            else
                Log.Warning("[HSKMoreHardcore] ManhunterDoorBreak: Pawn_HealthTracker.MakeDowned not found — хедифф снимается только при убийстве.");

            if (RageIcon == null)
                Log.Warning("[HSKMoreHardcore] ManhunterDoorBreak: текстура HMH/ManhunterRageMark не найдена — иконка ярости не рисуется.");

            Log.Message($"[HSKMoreHardcore] ManhunterDoorBreak applied. DebugLog={DebugLog}");
        }

        // Manhunter опрокинул (или сразу убил) любую пешку — «месть» утолена: снимаем
        // жажду мести с этого животного. Сама ярость (ментальное состояние)
        // остаётся; новый урон от колониста снова навесит хедифф.
        public static void DownedPostfix(Pawn ___pawn, DamageInfo? dinfo)
        {
            ClearRageByManhunter(dinfo, ___pawn, "опрокинул");
        }

        // Убийство без опрокидывания (сразу насмерть) — тоже снимаем
        public static void KillPostfix(Pawn __instance, DamageInfo? dinfo)
        {
            ClearRageByManhunter(dinfo, __instance, "убил");
        }

        private static void ClearRageByManhunter(DamageInfo? dinfo, Pawn victim, string what)
        {
            if (dinfo?.Instigator is not Pawn attacker || !IsManhunter(attacker))
                return;

            var rage = attacker.health?.hediffSet?.GetFirstHediffOfDef(RageDef);
            if (rage != null)
                attacker.health.RemoveHediff(rage);

            if (DebugLog)
                Log.Message($"[HSKMoreHardcore] ManhunterDoorBreak: {Describe(attacker)} {what} {Describe(victim)} -> жажда мести {(rage != null ? "СНЯТА" : "не была активна")}");
        }

        // Когда manhunter получает урон от колониста — вешаем жажду мести на него
        // и на всех manhunter в радиусе RageRadius от него
        public static void DamagePostfix(Pawn_MindState __instance, DamageInfo dinfo)
        {
            var pawn = __instance.pawn;
            if (pawn?.Map == null)
                return;

            if (!IsManhunter(pawn))
                return;

            // Только урон от пешки фракции игрока
            if (dinfo.Instigator is not Pawn attacker || attacker.Faction != Faction.OfPlayer)
                return;

            int enraged = 0, refreshed = 0;
            var pawns = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn other = pawns[i];
                if (!IsManhunter(other))
                    continue;
                if (!other.Position.InHorDistOf(pawn.Position, RageRadius))
                    continue;

                if (ApplyRage(other))
                    enraged++;
                else
                    refreshed++;
            }

            if (DebugLog)
                Log.Message($"[HSKMoreHardcore] ManhunterDoorBreak: {Describe(attacker)} ранил {Describe(pawn)} -> " +
                    $"жажда мести в радиусе {RageRadius}: новых {enraged}, продлено {refreshed}, на {AggroWindowTicks} тиков");
        }

        // Навесить/продлить жажду мести. true — хедифф новый, false — продлён существующий.
        private static bool ApplyRage(Pawn pawn)
        {
            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(RageDef);
            bool added = hediff == null;
            if (added)
                hediff = pawn.health.AddHediff(RageDef);

            var disappears = hediff.TryGetComp<HediffComp_Disappears>();
            if (disappears != null)
                disappears.ticksToDisappear = AggroWindowTicks;

            return added;
        }

        // Manhunter не нашёл цель — если у него жажда мести, ломать дверь
        public static void JobPostfix(ref Job __result, Pawn pawn)
        {
            // Только если оригинал не дал атаку (Goto/Wait/null = бродит или застрял)
            if (__result != null && __result.def != JobDefOf.Goto && __result.def != JobDefOf.Wait)
                return;

            if (!IsManhunter(pawn))
                return;

            if (pawn?.Map == null)
                return;

            string original = DescribeJob(__result);

            // Животных с дальней атакой (огнедышащие виверны и т.п.) сначала ведёт префикс
            // HSK SK.Patch_JobGiver_Manhunter_TryGiveJob: его Goto/Wait — выход на позицию
            // для стрельбы или к цели, а не «бродит у двери». Такие задачи не трогаем,
            // иначе животное на полпути к цели разворачивается бить дверь.
            // Если HSK сдался (return true), решает ванилла — её Goto/Wait у запертой
            // колонии перехватываем как у всех. Отличаем по меткам задач HSK:
            // Goto с checkOverrideOnExpire, Wait на 100 тиков (ванильный — 30).
            if (HasRangedVerb(pawn, out Verb rangedVerb) && IsHskRangedJob(__result))
            {
                LogSkip(pawn, "ranged-hsk", $"стрелок (verb {rangedVerb.verbProps.defaultProjectile?.defName ?? "?"}, range {rangedVerb.verbProps.range}), задача от HSK — не трогаем; {original}");
                return;
            }

            // Двери ломают только животные с жаждой мести
            if (pawn.health?.hediffSet?.GetFirstHediffOfDef(RageDef) == null)
            {
                LogSkip(pawn, "no-rage", $"нет жажды мести; ванилла: {original}");
                return;
            }

            // Сначала — есть ли пешка, до которой можно дойти БЕЗ ломания дверей
            // (кто-то остался на улице). Если есть — дерёмся с ней, двери не трогаем.
            // Ваниль так не умеет: она выбирает ближайшую цель, считая двери
            // проходимыми, и если та заперта — просто бродит рядом, не глядя
            // на досягаемых. Из-за этого зверь ломал дверь, когда рядом стоял колонист.
            Pawn openTarget = FindPawnTarget(pawn, canBashDoors: false);
            if (openTarget != null && pawn.CanReach(openTarget, PathEndMode.Touch, Danger.Deadly, canBashDoors: false, pawn.FenceBlocked))
            {
                // Параметры атаки — как у ванильного MeleeAttackJob манхантера
                Job attack = JobMaker.MakeJob(JobDefOf.AttackMelee, openTarget);
                attack.maxNumMeleeAttacks = 1;
                attack.expiryInterval = Rand.Range(420, 900);
                attack.attackDoorIfTargetLost = true;
                attack.canBashFences = pawn.FenceBlocked;
                __result = attack;

                if (DebugLog)
                {
                    lastLoggedDecision.Remove(pawn.thingIDNumber);
                    Log.Message($"[HSKMoreHardcore] ManhunterDoorBreak: {Describe(pawn)} -> ДОСЯГАЕМАЯ ЦЕЛЬ {Describe(openTarget)}, двери не трогаем; ванилла была: {original}");
                }
                return;
            }

            // Цель — как её выбирает сама ванилла. Ломаем двери, только если животное
            // идёт на пешку игрока; за чужими (рейдеры, гости) пусть гонится как обычно,
            // иначе оно разворачивается на полпути к ним и идёт бить дверь колонии.
            Pawn target = FindPawnTarget(pawn, canBashDoors: true);
            if (target == null || target.Faction != Faction.OfPlayer)
            {
                LogSkip(pawn, "not-player:" + (target?.thingIDNumber ?? 0),
                    $"цель не игрока: {(target == null ? "нет цели" : Describe(target))}; ванилла: {original}");
                return;
            }

            // Дверь — первая закрытая на реальном пути к цели. Проверки «колонист в 10
            // клетках» нет: если цель достижима, ванилла сама даёт атаку и сюда не доходит,
            // а по прямой сквозь стену она срывала атаку двери у самого порога.
            // «Ближайшая к цели дверь» тоже не годится: касание засчитывается по
            // диагонали, животное выбивало дверь из угла, но пройти в проём не могло;
            // при двух дверях подряд путь даёт их по порядку — внешнюю, затем внутреннюю.
            Building_Door door = FindDoorOnPath(pawn, target);
            if (door == null)
            {
                LogSkip(pawn, "no-door:" + target.thingIDNumber,
                    $"нет закрытой двери на пути; цель {Describe(target)}; ванилла: {original}");
                return;
            }

            Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, door);
            job.maxNumMeleeAttacks = 4;
            job.expiryInterval = 600;
            job.canBashDoors = true;
            __result = job;

            if (DebugLog)
            {
                lastLoggedDecision.Remove(pawn.thingIDNumber);
                Log.Message($"[HSKMoreHardcore] ManhunterDoorBreak: {Describe(pawn)} -> ДВЕРЬ {door.Label} @{door.Position} " +
                    $"(цель {Describe(target)}); ванилла была: {original}");
            }
        }

        // Иконки «!» над животными с жаждой мести на текущей карте. Вызывается из
        // MapComponent_ManhunterRageOverlay.MapComponentOnGUI — свой канал отрисовки,
        // не зависящий от чужих патчей на пешечные оверлеи (Camera+ и т.п.).
        // GUI-пространство (OnGUI), поэтому тултип через TipRegion работает.
        public static void DrawRageIcons(Map map)
        {
            if (RageIcon == null)
                return;

            CellRect viewRect = Find.CameraDriver.CurrentViewRect;
            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (!viewRect.Contains(pawn.Position))
                    continue;
                if (map.fogGrid.IsFogged(pawn.Position))
                    continue;
                if (!IsManhunter(pawn))
                    continue;
                if (pawn.health?.hediffSet?.GetFirstHediffOfDef(RageDef) == null)
                    continue;

                // Позиция и размер — один в один как у CEQuickLoadout
                // (Patch_PawnOverlay): фиксированные 20px, якорь — полклетки левее
                // и полклетки выше DrawPos, без привязки к размеру спрайта.
                const float iconSize = 20f;
                Vector3 world = pawn.DrawPos;
                world.x -= 0.5f;
                world.z += 0.5f;
                Vector2 pos = Find.Camera.WorldToScreenPoint(world) / Prefs.UIScale;
                pos.y = UI.screenHeight - pos.y;
                Rect rect = new Rect(pos.x - iconSize / 2f, pos.y - iconSize / 2f, iconSize, iconSize);

                // лёгкая пульсация — как у ванильных предупреждающих оверлеев
                float alpha = 0.7f + 0.3f * Mathf.Sin(Time.realtimeSinceStartup * 4f);
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(rect, RageIcon);
                GUI.color = Color.white;

                TooltipHandler.TipRegion(rect, "HSK_ManhunterRageTip".Translate());

                // Диагностика отрисовки: раз в ~2 сек
                if (DebugLog && Time.frameCount % 120 == 0)
                    Log.Message($"[HSKMoreHardcore] ManhunterDoorBreak: рисую «!» над {Describe(pawn)} rect={rect}, UIScale={Prefs.UIScale}");
            }
        }

        private static bool IsManhunter(Pawn pawn)
        {
            if (pawn?.MentalStateDef == null)
                return false;
            return pawn.MentalStateDef == MentalStateDefOf.Manhunter
                || pawn.MentalStateDef == MentalStateDefOf.ManhunterPermanent;
        }

        // Тот же признак, что в префиксе HSK: есть verb с дальностью больше 1.1
        private static bool HasRangedVerb(Pawn pawn, out Verb ranged)
        {
            ranged = null;
            var verbs = pawn.verbTracker?.AllVerbs;
            if (verbs == null)
                return false;
            for (int i = 0; i < verbs.Count; i++)
            {
                if (verbs[i].verbProps.range > 1.1f)
                {
                    ranged = verbs[i];
                    return true;
                }
            }
            return false;
        }

        // Goto/Wait, выданные префиксом HSK для стрелка (а не ванильным JobGiver_Manhunter):
        //  - Goto на позицию стрельбы: checkOverrideOnExpire = true (у ванильного false);
        //  - Wait на месте: JobMaker.MakeJob(Wait, 100) (ванильный — 30 тиков).
        private static bool IsHskRangedJob(Job job)
        {
            if (job == null)
                return false;
            if (job.def == JobDefOf.Goto)
                return job.checkOverrideOnExpire;
            if (job.def == JobDefOf.Wait)
                return job.expiryInterval == 100;
            return false;
        }

        // Копия JobGiver_Manhunter.FindPawnTarget: любая пешка с интеллектом.
        // canBashDoors=true — цели за дверями тоже годятся; false — только
        // досягаемые без ломания (для рукопашников BestAttackTarget сам
        // отфильтрует недостижимых).
        private static Pawn FindPawnTarget(Pawn pawn, bool canBashDoors)
        {
            return (Pawn)AttackTargetFinder.BestAttackTarget(pawn,
                TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable,
                x => x is Pawn && (int)x.def.race.intelligence >= 1,
                0f, 9999f, default(IntVec3), float.MaxValue,
                canBashDoors: canBashDoors, canTakeTargetsCloserThanEffectiveMinRange: true,
                canBashFences: pawn.FenceBlocked);
        }

        // Первая закрытая дверь на пути животного к цели (путь как у ванильного
        // JobGiver_Manhunter: TraverseMode.PassDoors). NodesReversed идёт от цели к
        // животному, поэтому перебираем с конца — от животного.
        private static Building_Door FindDoorOnPath(Pawn pawn, Pawn target)
        {
            var map = pawn.Map;
            if (map == null)
                return null;

            using (PawnPath path = map.pathFinder.FindPathNow(pawn.Position, target,
                TraverseParms.For(pawn, Danger.Deadly, TraverseMode.PassDoors), null, PathEndMode.Touch))
            {
                if (path == null || !path.Found)
                    return null;

                var nodes = path.NodesReversed;
                for (int i = nodes.Count - 1; i >= 0; i--)
                {
                    Building_Door door = nodes[i].GetDoor(map);
                    if (door != null && !door.Open)
                        return door;
                }
            }
            return null;
        }

        // category — короткий ключ без координат: по нему решаем, писать ли повтор
        private static void LogSkip(Pawn pawn, string category, string detail)
        {
            if (!DebugLog)
                return;
            if (lastLoggedDecision.TryGetValue(pawn.thingIDNumber, out string last) && last == category)
                return;
            lastLoggedDecision[pawn.thingIDNumber] = category;
            Log.Message($"[HSKMoreHardcore] ManhunterDoorBreak: {Describe(pawn)} пропуск: {detail}");
        }

        private static string Describe(Pawn p)
        {
            if (p == null)
                return "null";
            return $"{p.LabelShort}#{p.thingIDNumber} [{p.Faction?.Name ?? "без фракции"}] @{p.Position}";
        }

        private static string DescribeJob(Job job)
        {
            if (job == null)
                return "null";
            var t = job.targetA;
            string target = t.Thing != null ? $"{t.Thing.LabelShort}#{t.Thing.thingIDNumber} @{t.Thing.Position}" : t.Cell.ToString();
            return $"{job.def.defName} -> {target} (expiry={job.expiryInterval}, checkOverride={job.checkOverrideOnExpire})";
        }
    }

    /// <summary>
    /// Рисует «!» над животными с жаждой мести. MapComponent создаётся движком
    /// автоматически для каждой карты (ничего не сохраняет в сейв).
    /// </summary>
    public class MapComponent_ManhunterRageOverlay : MapComponent
    {
        public MapComponent_ManhunterRageOverlay(Map map) : base(map)
        {
        }

        public override void MapComponentOnGUI()
        {
            if (Event.current.type != EventType.Repaint)
                return;
            ManhunterDoorBreak.DrawRageIcons(map);
        }
    }
}
