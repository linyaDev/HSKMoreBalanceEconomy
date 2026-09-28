using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    // Убирает качество у строений: качество — это комп CompQuality в дефе,
    // без него нет ни ролла при постройке, ни лейбла, ни влияния на статы
    // (комфорт, отдых, красота, цена). Охват — категория Building: кровати,
    // мебель и т.п. Оружие и одежда (категория Item) не трогаются.
    // Исключение — арт (CompArt: скульптуры и т.п.): его крафтят мастера
    // навыком искусства, качество — суть предмета, оно остаётся.
    // Работает по DefDatabase после разрешения наследования — XML-патчем не
    // попасть, комп размазан по множеству абстрактных баз. У построек в сейве
    // сохранённое качество игнорируется при загрузке.
    [StaticConstructorOnStartup]
    public static class FurnitureQualityRemover
    {
        static FurnitureQualityRemover()
        {
            int removed = 0;
            int artKept = 0;
            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.category != ThingCategory.Building || def.comps == null)
                    continue;
                if (HasArtComp(def))
                {
                    artKept++;
                    continue;
                }
                removed += def.comps.RemoveAll(c => c?.compClass == typeof(CompQuality));
            }
            Log.Message($"[HSKMoreHardcore] FurnitureQualityRemover: quality removed from {removed} building defs, kept on {artKept} art defs.");
        }

        private static bool HasArtComp(ThingDef def)
        {
            for (int i = 0; i < def.comps.Count; i++)
            {
                if (def.comps[i] is CompProperties_Art)
                    return true;
            }
            return false;
        }
    }
}
