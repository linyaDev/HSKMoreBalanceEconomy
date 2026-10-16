using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace HSKMoreHardcore
{
    /// <summary>
    /// Если торговый караван фракции в гостях у игрока потерял 2+ вьючных
    /// животных (любая причина смерти), фракция обижается: платный вызов её
    /// каравана — консоль связи (CommsTraderRequest) и сигнальный костёр
    /// (CompTradeSignal) — недоступен 3 игровых года.
    /// Ванильный бесплатный запрос союзника не трогаем.
    /// Счёт ведётся на один визит (per-Lord), бан — per-faction, всё в сейве.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class CaravanAnimalDeathBan
    {
        public const int DeathsToBan = 2;
        public const int BanDurationTicks = 3 * 60 * 60000; // 3 игровых года

        static CaravanAnimalDeathBan()
        {
            var kill = AccessTools.Method(typeof(Pawn), nameof(Pawn.Kill));
            if (kill == null)
            {
                Log.Error("[HSKMoreHardcore] CaravanAnimalDeathBan: Pawn.Kill not found.");
                return;
            }

            new Harmony("linya.hskmorehardcore.caravananimaldeathban").Patch(kill,
                prefix: new HarmonyMethod(typeof(CaravanAnimalDeathBan), nameof(KillPrefix)),
                postfix: new HarmonyMethod(typeof(CaravanAnimalDeathBan), nameof(KillPostfix)));
        }

        // Лорда запоминаем до смерти: в постфиксе пешка уже выкинута из лорда
        public static void KillPrefix(Pawn __instance, out Lord __state)
        {
            __state = __instance.GetLord();
        }

        public static void KillPostfix(Pawn __instance, Lord __state)
        {
            if (__state == null || !__instance.Dead)
                return;
            if (!(__state.LordJob is LordJob_TradeWithColony))
                return;
            if (__instance.RaceProps == null || !__instance.RaceProps.packAnimal)
                return;

            Faction faction = __state.faction;
            if (faction == null || faction.IsPlayer)
                return;

            WorldComponent_CaravanAnimalDeaths.Instance?.RegisterPackAnimalDeath(__state, faction);
        }
    }

    public class WorldComponent_CaravanAnimalDeaths : WorldComponent
    {
        // Погибшие вьючные по визитам (loadID лорда -> число смертей)
        private Dictionary<int, int> deathsPerLord = new Dictionary<int, int>();

        // Бан платного вызова (loadID фракции -> тик окончания)
        private Dictionary<int, int> banEndTick = new Dictionary<int, int>();

        public static WorldComponent_CaravanAnimalDeaths Instance =>
            Find.World?.GetComponent<WorldComponent_CaravanAnimalDeaths>();

        public WorldComponent_CaravanAnimalDeaths(World world) : base(world)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref deathsPerLord, "deathsPerLord", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref banEndTick, "banEndTick", LookMode.Value, LookMode.Value);
            deathsPerLord ??= new Dictionary<int, int>();
            banEndTick ??= new Dictionary<int, int>();
        }

        public void RegisterPackAnimalDeath(Lord lord, Faction faction)
        {
            deathsPerLord.TryGetValue(lord.loadID, out int deaths);
            deaths++;
            deathsPerLord[lord.loadID] = deaths;

            Log.Message($"[HSKMoreHardcore] CaravanAnimalDeathBan: у каравана {faction.Name} (lord {lord.loadID}) погибло вьючных: {deaths}/{CaravanAnimalDeathBan.DeathsToBan}");

            // Срабатываем один раз — при пересечении порога
            if (deaths != CaravanAnimalDeathBan.DeathsToBan)
                return;

            banEndTick[faction.loadID] = Find.TickManager.TicksGame + CaravanAnimalDeathBan.BanDurationTicks;
            Messages.Message("HSK_CaravanAnimalBan".Translate(faction.Name,
                CaravanAnimalDeathBan.BanDurationTicks.ToStringTicksToPeriod()),
                MessageTypeDefOf.NegativeEvent);
        }

        public bool IsBanned(Faction faction, out int ticksLeft)
        {
            ticksLeft = 0;
            if (faction == null || !banEndTick.TryGetValue(faction.loadID, out int endTick))
                return false;

            ticksLeft = endTick - Find.TickManager.TicksGame;
            if (ticksLeft <= 0)
            {
                banEndTick.Remove(faction.loadID);
                return false;
            }
            return true;
        }
    }
}
