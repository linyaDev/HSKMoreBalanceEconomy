using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace HSKMoreHardcore
{
    /// <summary>
    /// На картах лагерей (WorldObject Camp) не генерятся рудные жилы — всегда,
    /// без оглядки на настройку campResources мода Set Up Camp.
    ///
    /// Почему настройка мода не работала в HSK: в ванили карту лагеря делает
    /// генератор Encounter с шагом RocksFromGrid_NoMinerals (maxMineableValue=0,
    /// жил нет), но Core_SK_CoreModule (SK_Patches_InsertGenSteps.xml) заменяет
    /// его на обычный RocksFromGrid — руда возвращается. Патч же Set Up Camp
    /// ждёт maxValue == 0 (ванильное значение), которого в HSK больше не бывает,
    /// и потому мёртв.
    ///
    /// RocksFromGrid раскидывает жилы через внутренний вызов
    /// GenStep_ScatterLumpsMineable.Generate — префикс на нём перехватывает и
    /// этот путь, и отдельные шаги рассыпки руды от других модов.
    /// Обычная скала, глубинные ресурсы и драгоценные жилы квестов не задеты.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class CampNoOre
    {
        static CampNoOre()
        {
            var target = AccessTools.Method(typeof(GenStep_ScatterLumpsMineable),
                nameof(GenStep_ScatterLumpsMineable.Generate),
                new[] { typeof(Map), typeof(GenStepParams) });
            if (target == null)
            {
                Log.Error("[HSKMoreHardcore] CampNoOre: GenStep_ScatterLumpsMineable.Generate not found.");
                return;
            }

            new Harmony("linya.hskmorehardcore.campnoore").Patch(target,
                prefix: new HarmonyMethod(typeof(CampNoOre), nameof(GeneratePrefix)));
            Log.Message("[HSKMoreHardcore] CampNoOre applied: лагеря без рудных жил.");
        }

        public static bool GeneratePrefix(GenStep_ScatterLumpsMineable __instance, Map map)
        {
            if (map?.Parent is Camp)
            {
                Log.Message($"[HSKMoreHardcore] CampNoOre: карта лагеря (тайл {map.Tile}) — жилы руды пропущены (maxValue был {__instance.maxValue}).");
                return false;
            }
            return true;
        }
    }
}
