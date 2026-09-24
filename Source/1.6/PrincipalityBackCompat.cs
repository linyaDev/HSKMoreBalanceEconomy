using RimWorld;
using RimWorld.Planet;
using Verse;

namespace HSKMoreHardcore
{
    // Подселяет Княжество (MedievalPrincipality) в существующие сейвы — тем же
    // механизмом, каким ванила добавляет DLC-фракции в старые миры
    // (BackCompatibility.FactionManagerPostLoadInit -> CreateFactionAndAddToManager),
    // но с диалогом подтверждения. Отказ сохраняется в сейве и повторно не
    // спрашивается. Ванильный путь не создаёт поселений — доспавниваем сами.
    // В новых мирах фракция появляется штатно (requiredCountAtGameStart).
    public class PrincipalityBackCompat : GameComponent
    {
        private const int SettlementCount = 2;

        private bool declined;

        public PrincipalityBackCompat(Game game)
        {
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref declined, "principalityDeclined", false);
        }

        public override void FinalizeInit()
        {
            if (declined)
                return;
            var def = DefDatabase<FactionDef>.GetNamedSilentFail("MedievalPrincipality");
            if (def == null)
                return;
            if (Find.FactionManager.FirstFactionOfDef(def) != null)
                return;

            LongEventHandler.ExecuteWhenFinished(() =>
            {
                Find.WindowStack.Add(new Dialog_MessageBox(
                    "В этом мире нет фракции «Княжество» — нейтральной средневековой фракции нов, которая шлёт торговые караваны.\n\nДобавить её и " + SettlementCount + " её поселения на карту мира?",
                    "Добавить", () => AddFaction(def),
                    "Нет", () => declined = true,
                    "HSK More Balance: Economy and Resources"));
            });
        }

        private void AddFaction(FactionDef def)
        {
            FactionGenerator.CreateFactionAndAddToManager(def);
            var faction = Find.FactionManager.FirstFactionOfDef(def);
            if (faction == null)
            {
                Log.Warning("[HSKMoreHardcore] PrincipalityBackCompat: faction generation failed.");
                return;
            }

            int spawned = 0;
            for (int i = 0; i < SettlementCount; i++)
            {
                var tile = TileFinder.RandomSettlementTileFor(faction);
                if (!tile.Valid)
                    continue;
                var settlement = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
                settlement.SetFaction(faction);
                settlement.Tile = tile;
                settlement.Name = SettlementNameGenerator.GenerateSettlementName(settlement);
                Find.WorldObjects.Add(settlement);
                spawned++;
            }

            Messages.Message($"Фракция «{faction.Name}» добавлена в мир ({spawned} поселения(й)).", MessageTypeDefOf.PositiveEvent, historical: false);
            Log.Message($"[HSKMoreHardcore] PrincipalityBackCompat: добавлена фракция {faction.Name} и {spawned} поселения(й).");
        }
    }
}
