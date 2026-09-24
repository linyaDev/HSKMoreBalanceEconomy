using HarmonyLib;
using Verse;

namespace HSKMoreHardcore
{
    // Время полёта метеорита Core_SK до удара.
    //
    // Событие MeteoriteImpact в паке идёт своим воркером
    // (SK.Events.IncidentWorker_MeteoriteImpact) и спавнит не скайфоллер, а вещь
    // класса SK.Events.MeteorIncoming. Тот в SpawnSetup выставляет себе
    // ticksToImpact = Rand.RangeInclusive(220, 300) — около четырёх секунд, и
    // значение зашито в коде, XML-патчем его не достать. Этим же классом падают
    // метеориты затмения (IncidentWorker_EclipseSubEventB) и метеоритный дождь
    // (Thing_MeteorSpawner).
    //
    // Переписываем значение постфиксом. Диапазон — meteorTicksToImpact
    // в HardcoreSettings.xml; пустой или нулевой оставляет паковое поведение.
    //
    // На отрисовку это влияет мягко: и сам камень, и тень показываются только
    // на последних ~200 тиках, так что долгий полёт просто означает паузу
    // между письмом и появлением тени.
    [StaticConstructorOnStartup]
    public static class MeteorFallDelay
    {
        private static AccessTools.FieldRef<object, int> ticksToImpactRef;

        static MeteorFallDelay()
        {
            var type = AccessTools.TypeByName("SK.Events.MeteorIncoming");
            var spawnSetup = type == null ? null : AccessTools.DeclaredMethod(type, "SpawnSetup");
            if (spawnSetup == null)
                return; // Core_SK не установлен или класс переименован

            var field = AccessTools.Field(type, "ticksToImpact");
            if (field == null)
            {
                Log.Warning("[HSKMoreHardcore] MeteorFallDelay: поле ticksToImpact не найдено — задержка метеоритов не работает.");
                return;
            }

            ticksToImpactRef = AccessTools.FieldRefAccess<int>(type, "ticksToImpact");

            new Harmony("linya.hskmorehardcore.meteorfalldelay").Patch(spawnSetup,
                postfix: new HarmonyMethod(typeof(MeteorFallDelay), nameof(SpawnSetupPostfix)));
        }

        public static void SpawnSetupPostfix(Thing __instance, bool respawningAfterLoad)
        {
            // При загрузке сейва значение приходит из сейва — не перетираем
            if (respawningAfterLoad || ticksToImpactRef == null || __instance == null)
                return;

            var range = HardcoreSettingsDef.Instance?.meteorTicksToImpact ?? default(IntRange);
            if (range.max <= 0)
                return;

            ticksToImpactRef(__instance) = range.RandomInRange;
        }
    }
}
