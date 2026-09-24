using System.Collections.Generic;
using Verse;

namespace HSKMoreHardcore
{
    // Вешает комп-метку на все дефы животных при старте игры, чтобы она
    // сохранялась между загрузками. В коде, а не XML: дефов животных в паке
    // много, а проверка по финальным дефам исключает задвоение компа через
    // наследование абстрактных дефов.
    [StaticConstructorOnStartup]
    public static class FreeAnimalCompInjector
    {
        static FreeAnimalCompInjector()
        {
            int injected = 0;
            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.race == null || !def.race.Animal)
                    continue;
                if (def.comps == null)
                    def.comps = new List<CompProperties>();

                bool already = false;
                foreach (var c in def.comps)
                {
                    if (c is CompProperties_FreeAnimal)
                    {
                        already = true;
                        break;
                    }
                }
                if (!already)
                {
                    def.comps.Add(new CompProperties_FreeAnimal());
                    injected++;
                }
            }
            Log.Message($"[HSKMoreHardcore] FreeAnimal comp injected into {injected} animal defs.");
        }
    }

    // Метка «досталось даром»: животное пришло событием, а не куплено и не выращено.
    // Влияет только на цену продажи (TraderAmmoNerf.PricePostfix).
    public class CompFreeAnimal : ThingComp
    {
        // Пришло событием (стадо, боевые и караванные животные, самоприручение)
        public bool joinedFree;
        // Приручено вручную с дикой карты
        public bool tamed;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref joinedFree, "joinedFree", false);
            Scribe_Values.Look(ref tamed, "tamed", false);
        }

        public override string CompInspectStringExtra()
        {
            if (joinedFree)
                return "HSKMoreHardcore_FreeAnimal".Translate();
            if (tamed)
                return "HSKMoreHardcore_TamedAnimal".Translate();
            return null;
        }
    }

    public class CompProperties_FreeAnimal : CompProperties
    {
        public CompProperties_FreeAnimal()
        {
            compClass = typeof(CompFreeAnimal);
        }
    }
}
