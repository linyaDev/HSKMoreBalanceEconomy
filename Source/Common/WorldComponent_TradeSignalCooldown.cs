using System.Collections.Generic;
using RimWorld.Planet;
using Verse;

namespace HSKMoreHardcore
{
    public class WorldComponent_TradeSignalCooldown : WorldComponent
    {
        private Dictionary<string, int> charges = new Dictionary<string, int>();
        private Dictionary<string, int> rechargeStartTick = new Dictionary<string, int>();

        public WorldComponent_TradeSignalCooldown(World world) : base(world)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref charges, "charges", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref rechargeStartTick, "rechargeStartTick", LookMode.Value, LookMode.Value);
            charges ??= new Dictionary<string, int>();
            rechargeStartTick ??= new Dictionary<string, int>();
        }

        public int GetCharges(string key, int maxCharges, int cooldownTicks)
        {
            if (!charges.ContainsKey(key))
                return maxCharges;

            int stored = charges[key];
            if (stored >= maxCharges)
                return maxCharges;

            if (!rechargeStartTick.TryGetValue(key, out int startTick))
                return stored;

            int elapsed = Find.TickManager.TicksGame - startTick;
            int recharged = elapsed / cooldownTicks;

            if (recharged > 0)
            {
                stored = System.Math.Min(stored + recharged, maxCharges);
                charges[key] = stored;

                if (stored >= maxCharges)
                    rechargeStartTick.Remove(key);
                else
                    rechargeStartTick[key] = startTick + recharged * cooldownTicks;
            }

            return stored;
        }

        public int GetTicksToNextCharge(string key, int maxCharges, int cooldownTicks)
        {
            int current = GetCharges(key, maxCharges, cooldownTicks);
            if (current >= maxCharges)
                return 0;

            if (!rechargeStartTick.TryGetValue(key, out int startTick))
                return 0;

            int elapsed = Find.TickManager.TicksGame - startTick;
            int remainder = elapsed % cooldownTicks;
            return cooldownTicks - remainder;
        }

        public void ConsumeCharge(string key, int maxCharges, int cooldownTicks)
        {
            int current = GetCharges(key, maxCharges, cooldownTicks);
            current = System.Math.Max(0, current - 1);
            charges[key] = current;

            if (!rechargeStartTick.ContainsKey(key))
                rechargeStartTick[key] = Find.TickManager.TicksGame;
        }
    }
}
