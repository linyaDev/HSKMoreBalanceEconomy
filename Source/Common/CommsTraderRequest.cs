using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    // Платный вызов торгового каравана с консоли связи.
    //
    // Ваниль даёт «запросить торговца» только союзникам (FactionDialogMaker.
    // RequestTraderOption отключает строку с подписью MustBeAlly). Мы добавляем
    // вторую строку для нейтральных фракций: тот же караван, но за серебро
    // и репутацию, с длинной перезарядкой на фракцию.
    //
    // Откат храним в ванильном faction.lastTraderRequestTick — он уже лежит
    // в сейве и общий с союзным запросом: подряд два каравана от одной фракции
    // не выпросить.
    [StaticConstructorOnStartup]
    public static class CommsTraderRequest
    {
        static CommsTraderRequest()
        {
            var target = AccessTools.Method(typeof(FactionDialogMaker), "FactionDialogFor");
            if (target == null)
            {
                Log.Error("[HSKMoreHardcore] FactionDialogMaker.FactionDialogFor not found.");
                return;
            }

            new Harmony("linya.hskmorehardcore.commstraderrequest").Patch(target,
                postfix: new HarmonyMethod(typeof(CommsTraderRequest), nameof(AddPaidTraderOption)));
        }

        private static HardcoreSettingsDef Settings => HardcoreSettingsDef.Instance;

        public static void AddPaidTraderOption(DiaNode __result, Pawn negotiator, Faction faction)
        {
            var settings = Settings;
            if (settings == null || settings.commsTraderSilverCost <= 0)
                return;
            if (__result?.options == null || negotiator == null || faction == null)
                return;

            Map map = negotiator.Map;
            if (map == null || !map.IsPlayerHome)
                return;
            if (!IsEligible(faction, settings))
                return;

            DiaOption option = BuildOption(map, faction, negotiator, settings);
            if (option == null)
                return;

            if (negotiator.skills != null && negotiator.skills.GetSkill(SkillDefOf.Social).TotallyDisabled)
                option.Disable("WorkTypeDisablesOption".Translate(SkillDefOf.Social.label));

            __result.options.Insert(InsertIndex(__result, faction), option);
        }

        // Текст строки диалога закрыт (protected), но нам нужно узнать ванильный
        // запрос торговца в списке, чтобы встать сразу за ним
        private static readonly AccessTools.FieldRef<DiaOption, string> optionTextRef =
            AccessTools.FieldRefAccess<DiaOption, string>("text");

        // Ставим нашу строку сразу после ванильного «запросить торговца»
        // (у нейтральных он висит серым с подписью MustBeAlly). Не нашли —
        // перед «Отключиться»: она всегда последняя и закрывает диалог.
        private static int InsertIndex(DiaNode node, Faction faction)
        {
            string vanillaText = "RequestTrader".Translate(
                -Faction.OfPlayer.CalculateAdjustedGoodwillChange(faction, -15)).Resolve();

            for (int i = 0; i < node.options.Count; i++)
            {
                if (optionTextRef(node.options[i]) == vanillaText)
                    return i + 1;
            }

            int disconnect = node.options.FindIndex(o => o.resolveTree);
            return disconnect < 0 ? node.options.Count : disconnect;
        }

        private static bool IsEligible(Faction faction, HardcoreSettingsDef settings)
        {
            if (faction.IsPlayer || faction.defeated || faction.def.hidden)
                return false;
            // Союзникам караван и так доступен бесплатно, врагам — никак
            if (faction.PlayerRelationKind != FactionRelationKind.Neutral)
                return false;
            if (!faction.def.canRequestTraders || faction.def.caravanTraderKinds.NullOrEmpty())
                return false;
            if (settings.commsTraderMaxTechLevel != TechLevel.Undefined
                && faction.def.techLevel > settings.commsTraderMaxTechLevel)
                return false;
            if (settings.commsTraderExcludedFactions != null
                && settings.commsTraderExcludedFactions.Contains(faction.def.defName))
                return false;
            return true;
        }

        private static DiaOption BuildOption(Map map, Faction faction, Pawn negotiator, HardcoreSettingsDef settings)
        {
            int silverCost = settings.commsTraderSilverCost;
            int goodwillCost = settings.commsTraderGoodwillCost;
            int charges = GetCharges(settings);
            TaggedString label = "HSK_RequestTraderPaid".Translate(
                silverCost, -Faction.OfPlayer.CalculateAdjustedGoodwillChange(faction, -goodwillCost),
                charges, settings.commsTraderMaxCharges);

            if (!faction.def.allowedArrivalTemperatureRange.ExpandedBy(-4f).Includes(map.mapTemperature.SeasonalTemp))
            {
                var disabled = new DiaOption(label);
                disabled.Disable("BadTemperature".Translate());
                return disabled;
            }

            if (charges <= 0)
            {
                var disabled = new DiaOption(label);
                disabled.Disable("WaitTime".Translate(TicksToNextCharge(settings).ToStringTicksToPeriod()));
                return disabled;
            }

            if (NeutralGroupIncidentUtility.AnyBlockingHostileLord(map, faction))
            {
                var disabled = new DiaOption(label);
                disabled.Disable("HostileVisitorsPresent".Translate());
                return disabled;
            }

            int silver = ColonySilver.CountOnMap(map);
            if (silver < silverCost)
            {
                var disabled = new DiaOption(label);
                disabled.Disable("TradeSignal_NotEnoughSilver".Translate(silverCost, silver));
                return disabled;
            }

            var option = new DiaOption(label);
            var sent = new DiaNode("HSK_TraderPaidSent".Translate(faction.Name, silverCost));
            sent.options.Add(new DiaOption("OK".Translate())
            {
                linkLateBind = FactionDialogMaker.ResetToRoot(faction, negotiator)
            });

            var chooseKind = new DiaNode("ChooseTraderKind".Translate(faction.Name));
            foreach (TraderKindDef kind in faction.def.caravanTraderKinds.Where(x => x.requestable))
            {
                TraderKindDef localKind = kind;
                var kindOption = new DiaOption(localKind.LabelCap);

                if (localKind.TitleRequiredToTrade != null
                    && (negotiator.royalty == null
                        || localKind.TitleRequiredToTrade.seniority > negotiator.GetCurrentTitleSeniorityIn(faction)))
                {
                    var denied = new DiaNode("TradeCaravanRequestDeniedDueTitle".Translate(
                        negotiator.Named("NEGOTIATOR"),
                        localKind.TitleRequiredToTrade.GetLabelCapFor(negotiator).Named("TITLE"),
                        faction.Named("FACTION")));
                    denied.options.Add(new DiaOption("GoBack".Translate()) { link = chooseKind });
                    kindOption.link = denied;
                }
                else
                {
                    kindOption.action = delegate
                    {
                        SendPaidTrader(map, faction, localKind, settings);
                    };
                    kindOption.link = sent;
                }

                chooseKind.options.Add(kindOption);
            }

            chooseKind.options.Add(new DiaOption("GoBack".Translate())
            {
                linkLateBind = FactionDialogMaker.ResetToRoot(faction, negotiator)
            });

            option.link = chooseKind;
            return option;
        }

        // Заряды общие на все фракции и живут в том же трекере, что и заряды
        // сигнального костра: следить за таймером не нужно, запас копится сам.
        private const string ChargeKey = "commsTrader";

        private static WorldComponent_TradeSignalCooldown Tracker =>
            Find.World?.GetComponent<WorldComponent_TradeSignalCooldown>();

        private static int GetCharges(HardcoreSettingsDef settings)
        {
            var tracker = Tracker;
            if (tracker == null)
                return settings.commsTraderMaxCharges;
            return tracker.GetCharges(ChargeKey, settings.commsTraderMaxCharges, settings.commsTraderCooldownTicks);
        }

        private static int TicksToNextCharge(HardcoreSettingsDef settings)
        {
            var tracker = Tracker;
            if (tracker == null)
                return 0;
            return tracker.GetTicksToNextCharge(ChargeKey, settings.commsTraderMaxCharges, settings.commsTraderCooldownTicks);
        }

        private static void SendPaidTrader(Map map, Faction faction, TraderKindDef traderKind, HardcoreSettingsDef settings)
        {
            int silverCost = settings.commsTraderSilverCost;
            if (GetCharges(settings) <= 0)
                return;
            // Серебро берём со складов карты, а не из зоны орбитального маяка
            if (!ColonySilver.TakeFromMap(map, silverCost))
            {
                Messages.Message("TradeSignal_NotEnoughSilver".Translate(silverCost, ColonySilver.CountOnMap(map)),
                    MessageTypeDefOf.RejectInput);
                return;
            }

            var parms = new IncidentParms
            {
                target = map,
                faction = faction,
                traderKind = traderKind,
                forced = true
            };
            Find.Storyteller.incidentQueue.Add(IncidentDefOf.TraderCaravanArrival,
                Find.TickManager.TicksGame + settings.commsTraderArrivalDelayTicks, parms, 240000);

            faction.lastTraderRequestTick = Find.TickManager.TicksGame;
            Faction.OfPlayer.TryAffectGoodwillWith(faction, -settings.commsTraderGoodwillCost,
                canSendMessage: false, canSendHostilityLetter: true, HistoryEventDefOf.RequestedTrader);

            Tracker?.ConsumeCharge(ChargeKey, settings.commsTraderMaxCharges, settings.commsTraderCooldownTicks);
        }
    }
}
