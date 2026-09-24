using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKMoreHardcore
{
    public class CompProperties_TradeSignal : CompProperties
    {
        public int cooldownTicks = 900000;
        public int maxCharges = 1;
        public int silverCost = 200;
        public int arrivalDelayTicks = 120000;
        public TechLevel targetTechLevel = TechLevel.Neolithic;
        public bool destroyOnUse = true;
        public TechLevel disableAtTechLevel = TechLevel.Undefined;
        public string cooldownKey = "tribal";

        // Отсрочка уже вызванного каравана: сколько стоит и на сколько сдвигает
        public int delaySilverCost = 100;
        public int delayTicks = 60000;

        public string commandLabelKey = "TribalSignal_CommandLabel";
        public string commandDescKey = "TribalSignal_CommandDesc";
        public string delayCommandLabelKey = "TradeSignal_DelayCommandLabel";
        public string delayCommandDescKey = "TradeSignal_DelayCommandDesc";
        // Ванильная иконка «придержать караван»; не нашлась — берём значок постройки
        public string delayCommandIcon = "UI/Commands/PauseCaravan";
        public string scheduledKey = "TribalSignal_Scheduled";
        public string noFactionKey = "TribalSignal_NoFaction";
        public string activeKey = "TribalSignal_Burning";
        public string doneKey = "TribalSignal_BurnedOut";

        public CompProperties_TradeSignal()
        {
            compClass = typeof(CompTradeSignal);
        }
    }

    public class CompTradeSignal : ThingComp
    {
        private bool isActive;
        private int arrivalTick = -1;

        public bool IsActive => isActive;

        private CompProperties_TradeSignal Props => (CompProperties_TradeSignal)props;

        private static WorldComponent_TradeSignalCooldown Tracker =>
            Find.World?.GetComponent<WorldComponent_TradeSignalCooldown>();

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref isActive, "isActive");
            Scribe_Values.Look(ref arrivalTick, "arrivalTick", -1);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!isActive || parent.Map == null)
                return;

            if (Props.destroyOnUse)
            {
                Vector3 pos = parent.DrawPos;
                Map map = parent.Map;

                if (parent.IsHashIntervalTick(15))
                    FleckMaker.ThrowFireGlow(pos, map, 1.5f);

                if (parent.IsHashIntervalTick(30))
                    FleckMaker.ThrowSmoke(pos + new Vector3(0f, 0f, 0.5f), map, 2f);

                if (parent.IsHashIntervalTick(50))
                    FleckMaker.ThrowMicroSparks(pos, map);
            }

            if (arrivalTick > 0 && Find.TickManager.TicksGame >= arrivalTick)
            {
                isActive = false;
                UpdateGlow();
                Messages.Message(Props.doneKey.Translate(), MessageTypeDefOf.NeutralEvent);
                if (Props.destroyOnUse)
                {
                    parent.Destroy(DestroyMode.Vanish);
                }
            }
        }

        private Texture2D cachedDelayIcon;

        private Texture2D DelayIcon
        {
            get
            {
                if (cachedDelayIcon == null)
                    cachedDelayIcon = ContentFinder<Texture2D>.Get(Props.delayCommandIcon, reportFailure: false)
                        ?? parent.def.uiIcon;
                return cachedDelayIcon;
            }
        }

        // Пересчитать свечение костра (CompGlower) после смены состояния «зажжён»
        private void UpdateGlow()
        {
            if (parent.Spawned)
                parent.GetComp<CompGlower>()?.UpdateLit(parent.Map);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent is not Building || !parent.Spawned || parent.Map == null)
                yield break;

            // Пока караван в пути — только кнопка отсрочки
            if (isActive)
            {
                if (arrivalTick > 0)
                {
                    var delayCmd = new Command_Action
                    {
                        defaultLabel = Props.delayCommandLabelKey.Translate(),
                        defaultDesc = Props.delayCommandDescKey.Translate(Props.delaySilverCost,
                            ((float)Props.delayTicks / GenDate.TicksPerDay).ToString("F1")),
                        icon = DelayIcon,
                        action = TryDelayArrival
                    };

                    int silver = ColonySilver.CountOnMap(parent.Map);
                    if (silver < Props.delaySilverCost)
                        delayCmd.Disable("TradeSignal_NotEnoughSilver".Translate(Props.delaySilverCost, silver));

                    yield return delayCmd;
                }
                yield break;
            }

            var cmd = new Command_Action
            {
                defaultLabel = Props.commandLabelKey.Translate(),
                defaultDesc = Props.commandDescKey.Translate(Props.silverCost),
                icon = parent.def.uiIcon,
                action = TryCallTrader
            };

            int charges = GetCharges();
            if (IsDisabledByProgress())
            {
                cmd.Disable("TradeSignal_TooAdvancedStatus".Translate());
            }
            else if (charges <= 0)
            {
                int ticksLeft = GetTicksToNextCharge();
                cmd.Disable("TradeSignal_OnCooldown".Translate(ticksLeft.ToStringTicksToPeriod()));
            }
            else if (!AnyValidTradeFaction(parent.Map))
            {
                cmd.Disable(Props.noFactionKey.Translate());
            }

            yield return cmd;
        }

        public override string CompInspectStringExtra()
        {
            if (isActive && arrivalTick > 0)
            {
                int ticksLeft = arrivalTick - Find.TickManager.TicksGame;
                if (ticksLeft > 0)
                    return Props.activeKey.Translate(ticksLeft.ToStringTicksToPeriod());
            }

            if (IsDisabledByProgress())
                return "TradeSignal_TooAdvancedStatus".Translate();

            int charges = GetCharges();
            if (charges < Props.maxCharges)
            {
                int ticksToNext = GetTicksToNextCharge();
                return "TradeSignal_Charges".Translate(charges, Props.maxCharges)
                    + "\n" + "TradeSignal_OnCooldown".Translate(ticksToNext.ToStringTicksToPeriod());
            }

            return "TradeSignal_Charges".Translate(charges, Props.maxCharges);
        }

        private int GetCharges()
        {
            var tracker = Tracker;
            if (tracker == null)
                return Props.maxCharges;
            return tracker.GetCharges(Props.cooldownKey, Props.maxCharges, Props.cooldownTicks);
        }

        private int GetTicksToNextCharge()
        {
            var tracker = Tracker;
            if (tracker == null)
                return 0;
            return tracker.GetTicksToNextCharge(Props.cooldownKey, Props.maxCharges, Props.cooldownTicks);
        }

        // Отодвинуть прибытие уже вызванного каравана. Событие лежит в очереди
        // рассказчика (QueuedIncident), поле fireTick закрыто — правим рефлексией:
        // IncidentQueue каждый тик просто сравнивает FireTick с текущим, порядок
        // элементов значения не имеет, поэтому переставлять ничего не нужно.
        private void TryDelayArrival()
        {
            Map map = parent.Map;
            if (map == null || !isActive || arrivalTick <= 0)
                return;

            var queued = FindQueuedArrival(map);
            if (queued == null)
            {
                // Событие уже ушло из очереди (сработало или карта сменилась)
                Messages.Message("TradeSignal_DelayFailed".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            if (!ColonySilver.TakeFromMap(map, Props.delaySilverCost))
            {
                Messages.Message("TradeSignal_NotEnoughSilver".Translate(Props.delaySilverCost, ColonySilver.CountOnMap(map)), MessageTypeDefOf.RejectInput);
                return;
            }

            int newTick = queued.FireTick + Props.delayTicks;
            fireTickRef(queued) = newTick;
            arrivalTick = newTick;

            int ticksLeft = arrivalTick - Find.TickManager.TicksGame;
            Messages.Message("TradeSignal_Delayed".Translate(ticksLeft.ToStringTicksToPeriod()), MessageTypeDefOf.PositiveEvent);
        }

        private static readonly AccessTools.FieldRef<QueuedIncident, int> fireTickRef =
            AccessTools.FieldRefAccess<QueuedIncident, int>("fireTick");

        // Наше событие в очереди: тот же деф, та же карта и тот же тик прибытия
        private QueuedIncident FindQueuedArrival(Map map)
        {
            var queue = Find.Storyteller?.incidentQueue;
            if (queue == null)
                return null;

            var incident = DefDatabase<IncidentDef>.GetNamedSilentFail("TraderCaravanArrival");
            var enumerator = queue.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (enumerator.Current is not QueuedIncident qi)
                    continue;
                if (qi.FireTick != arrivalTick)
                    continue;
                var firing = qi.FiringIncident;
                if (firing == null || (incident != null && firing.def != incident))
                    continue;
                if (firing.parms?.target != map)
                    continue;
                return qi;
            }
            return null;
        }

        private void TryCallTrader()
        {
            Map map = parent.Map;
            if (map == null) return;
            if (GetCharges() <= 0) return;

            // Кнопка в этом случае уже неактивна — страховка на случай вызова извне.
            // Заряд не тратим: постройка просто больше не работает.
            if (IsDisabledByProgress())
            {
                Messages.Message("TradeSignal_TooAdvanced".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            List<Faction> candidates = FindValidTradeFactions(map).ToList();
            if (candidates.Count == 0)
            {
                Messages.Message(Props.noFactionKey.Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            int silverAvailable = ColonySilver.CountOnMap(map);
            if (silverAvailable < Props.silverCost)
            {
                Messages.Message("TradeSignal_NotEnoughSilver".Translate(Props.silverCost, silverAvailable), MessageTypeDefOf.RejectInput);
                return;
            }

            // Выбор типа торговца: объединение caravanTraderKinds всех подходящих фракций
            var options = new List<FloatMenuOption>();
            var seenKinds = new List<TraderKindDef>();
            foreach (Faction f in candidates)
            {
                foreach (TraderKindDef kind in f.def.caravanTraderKinds)
                {
                    if (seenKinds.Contains(kind))
                        continue;
                    seenKinds.Add(kind);
                    TraderKindDef k = kind;
                    options.Add(new FloatMenuOption(k.LabelCap, delegate { ConfirmAndExecute(map, candidates, k); }));
                }
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ConfirmAndExecute(Map map, List<Faction> candidates, TraderKindDef traderKind)
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "TradeSignal_ConfirmPayment".Translate(Props.silverCost),
                delegate { ExecuteSignal(map, candidates, traderKind); }));
        }

        private void ExecuteSignal(Map map, List<Faction> candidates, TraderKindDef traderKind)
        {
            if (!ColonySilver.TakeFromMap(map, Props.silverCost))
            {
                Messages.Message("TradeSignal_NotEnoughSilver".Translate(Props.silverCost, ColonySilver.CountOnMap(map)), MessageTypeDefOf.RejectInput);
                return;
            }

            // Если выбран конкретный тип — берём фракцию, у которой он есть
            if (traderKind != null)
            {
                List<Faction> withKind = candidates.Where(f => f.def.caravanTraderKinds.Contains(traderKind)).ToList();
                if (withKind.Count > 0)
                    candidates = withKind;
                else
                    traderKind = null; // на всякий случай: тип пропал — прежнее поведение
            }

            Faction faction = candidates.RandomElement();
            IncidentParms parms = new IncidentParms
            {
                target = map,
                faction = faction,
                forced = true,
                traderKind = traderKind
            };

            IncidentDef incident = DefDatabase<IncidentDef>.GetNamedSilentFail("TraderCaravanArrival");
            if (incident == null)
            {
                Log.Error("[HSKMoreHardcore] TraderCaravanArrival incident not found.");
                return;
            }

            int fireTick = Find.TickManager.TicksGame + Props.arrivalDelayTicks;
            Find.Storyteller.incidentQueue.Add(incident, fireTick, parms);

            isActive = true;
            arrivalTick = fireTick;
            UpdateGlow();

            string delayDaysStr = ((float)Props.arrivalDelayTicks / GenDate.TicksPerDay).ToString("F1");
            TaggedString scheduledMsg = Props.scheduledKey.Translate(faction.Name, delayDaysStr);
            if (traderKind != null)
                scheduledMsg += " (" + traderKind.LabelCap + ")";
            Messages.Message(scheduledMsg, MessageTypeDefOf.PositiveEvent);

            Tracker?.ConsumeCharge(Props.cooldownKey, Props.maxCharges, Props.cooldownTicks);
        }

        // Техуровень игрока берём из IgnoranceCompat (Ignorance Is Bliss, по прогрессу
        // исследований), а не из фракции: та скачет от Tech Advancing и отключала
        // костёр раньше времени. Без Ignorance Is Bliss — как раньше, по фракции.
        private bool IsDisabledByProgress()
        {
            if (Props.disableAtTechLevel == TechLevel.Undefined)
                return false;

            TechLevel playerTech = IgnoranceCompat.PlayerTechLevel;
            if (playerTech == TechLevel.Undefined)
                return false;

            return playerTech >= Props.disableAtTechLevel;
        }

        private bool AnyValidTradeFaction(Map map)
        {
            return FindValidTradeFactions(map).Any();
        }

        private List<Faction> FindValidTradeFactions(Map map)
        {
            return FindFactionsOfTechLevel(map, Props.targetTechLevel);
        }

        private static List<Faction> FindFactionsOfTechLevel(Map map, TechLevel techLevel)
        {
            List<Faction> result = new List<Faction>();
            foreach (Faction f in Find.FactionManager.AllFactions)
            {
                if (IsValidTradeSource(f, map, techLevel))
                    result.Add(f);
            }
            return result;
        }

        private static bool IsValidTradeSource(Faction f, Map map, TechLevel techLevel)
        {
            if (f.IsPlayer || f.defeated || f.def.hidden)
                return false;
            if (f.def.techLevel != techLevel)
                return false;
            if (f.def.caravanTraderKinds.NullOrEmpty())
                return false;
            if (f.HostileTo(Faction.OfPlayer))
                return false;
            if (!f.def.allowedArrivalTemperatureRange.Includes(map.mapTemperature.OutdoorTemp))
                return false;
            if (!f.def.allowedArrivalTemperatureRange.Includes(map.mapTemperature.SeasonalTemp))
                return false;
            if (NeutralGroupIncidentUtility.AnyBlockingHostileLord(map, f))
                return false;

            return true;
        }
    }
}
