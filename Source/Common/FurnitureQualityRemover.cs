using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    // Убирает качество у всех строений: качество — это комп CompQuality в дефе,
    // без него нет ни ролла при постройке, ни лейбла, ни влияния на статы
    // (комфорт, отдых, красота, цена). Охват — вся категория Building, включая
    // кровати, мебель и скульптуры. Оружие и одежда (категория Item) не трогаются.
    // Работает по DefDatabase после разрешения наследования — XML-патчем не
    // попасть, комп размазан по множеству абстрактных баз. У построек в сейве
    // сохранённое качество игнорируется при загрузке.
    [StaticConstructorOnStartup]
    public static class FurnitureQualityRemover
    {
        static FurnitureQualityRemover()
        {
            int removed = 0;
            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.category != ThingCategory.Building || def.comps == null)
                    continue;
                removed += def.comps.RemoveAll(c => c?.compClass == typeof(CompQuality));
            }
            Log.Message($"[HSKMoreHardcore] FurnitureQualityRemover: quality removed from {removed} building defs.");
        }
    }
}
