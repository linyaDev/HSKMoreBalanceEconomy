using System.Collections.Generic;
using Verse;

namespace HSKMoreHardcore
{
    // Привязывает CompProperties_WornByEnemy ко всем дефам одежды при старте игры,
    // чтобы метка «со следами боя» (worn/wornByEnemy) сохранялась между загрузками.
    // Делаем в коде, а не XML: проверка по финальным дефам исключает задвоение компа
    // через наследование абстрактных дефов (которое ломало бы скрайб при загрузке).
    // Старт происходит до загрузки любого сейва, так что вещи получают комп при загрузке.
    [StaticConstructorOnStartup]
    public static class WornByEnemyCompInjector
    {
        static WornByEnemyCompInjector()
        {
            int injected = 0;
            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                // Одежда — метки износа/стирки; оружие — счётчик починок «Починено».
                if (!def.IsApparel && !def.IsWeapon)
                    continue;
                if (def.comps == null)
                    def.comps = new List<CompProperties>();
                bool already = false;
                foreach (var c in def.comps)
                {
                    if (c is CompProperties_WornByEnemy)
                    {
                        already = true;
                        break;
                    }
                }
                if (!already)
                {
                    def.comps.Add(new CompProperties_WornByEnemy());
                    injected++;
                }
            }
            Log.Message($"[HSKMoreHardcore] WornByEnemy comp injected into {injected} apparel/weapon defs.");
        }
    }

    public class CompWornByEnemy : ThingComp
    {
        public bool worn;
        public bool wornByEnemy;
        public bool washed;
        // Счётчик починок. Независим от меток износа/стирки — может стоять одновременно с ними.
        public int repairCount;

        // Помечена ли вещь как поношенная (можно постирать)
        public bool IsWornMarked => worn || wornByEnemy;

        // Можно ли чинить обычной починкой (Mend & Recycle)
        public bool CanMendNormally => repairCount < NerfSettings.repairBlockThreshold;

        // Постирать: снять метки износа, поставить «постирано»
        public void Wash()
        {
            worn = false;
            wornByEnemy = false;
            washed = true;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref worn, "worn", false);
            Scribe_Values.Look(ref wornByEnemy, "wornByEnemy", false);
            Scribe_Values.Look(ref washed, "washed", false);
            Scribe_Values.Look(ref repairCount, "repairCount", 0);
        }

        public override string CompInspectStringExtra()
        {
            var parts = new List<string>();
            if (washed)
                parts.Add("HSKMoreHardcore_Washed".Translate());
            if (wornByEnemy)
                parts.Add("HSKMoreHardcore_WornByEnemy".Translate());
            if (worn)
                parts.Add("HSKMoreHardcore_Worn".Translate());
            if (repairCount > 0)
                parts.Add("HSKMoreHardcore_Repaired".Translate() + " x" + repairCount);
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        public override string GetDescriptionPart()
        {
            var parts = new List<string>();
            if (washed)
                parts.Add("HSKMoreHardcore_WashedDesc".Translate());
            if (wornByEnemy)
                parts.Add("HSKMoreHardcore_WornByEnemyDesc".Translate());
            if (worn)
                parts.Add("HSKMoreHardcore_WornDesc".Translate());
            if (repairCount > 0)
                parts.Add(("HSKMoreHardcore_RepairedDesc".Translate()) + " (x" + repairCount + ")");
            return parts.Count > 0 ? string.Join("\n", parts) : null;
        }
    }

    public class CompProperties_WornByEnemy : CompProperties
    {
        public CompProperties_WornByEnemy()
        {
            compClass = typeof(CompWornByEnemy);
        }
    }
}
