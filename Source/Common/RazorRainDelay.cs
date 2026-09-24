using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace HSKMoreHardcore
{
    // Стальной дождь: пауза между письмом и первыми осколками.
    //
    // Событие (SK.Events.IncidentWorker_RazorRain) спавнит на карту невидимый
    // Thing_RazorSpawner и сразу шлёт письмо. Спавнер в TickRare (раз в 250 тиков)
    // с шансом 50% сбрасывает пачку из 1-30 осколков, каждый из которых летит
    // 120-200 тиков. То есть первые попадания возможны уже через несколько секунд
    // после письма — спрятаться не успеть.
    //
    // Держим спавнер выключенным razorRainStartDelayTicks тиков с момента появления.
    // Само количество пачек не меняется: счётчик numRazorEventCount уменьшается
    // только при сбросе, так что дождь не становится короче — он просто начинается
    // позже.
    [StaticConstructorOnStartup]
    public static class RazorRainDelay
    {
        // thingIDNumber спавнера -> тик, когда он появился
        private static readonly Dictionary<int, int> spawnTicks = new Dictionary<int, int>();

        static RazorRainDelay()
        {
            var type = AccessTools.TypeByName("SK.Events.Thing_RazorSpawner");
            var tickRare = type == null ? null : AccessTools.DeclaredMethod(type, "TickRare");
            if (tickRare == null)
                return; // Core_SK не установлен или класс переименован

            new Harmony("linya.hskmorehardcore.razorraindelay").Patch(tickRare,
                prefix: new HarmonyMethod(typeof(RazorRainDelay), nameof(TickRarePrefix)));
        }

        // false — пропустить тик спавнера
        public static bool TickRarePrefix(Thing __instance)
        {
            int delay = HardcoreSettingsDef.Instance?.razorRainStartDelayTicks ?? 0;
            if (delay <= 0 || __instance == null)
                return true;

            int now = Find.TickManager.TicksGame;
            if (!spawnTicks.TryGetValue(__instance.thingIDNumber, out int start))
            {
                // Первый тик после появления или после загрузки сейва
                ForgetOldEntries(now);
                spawnTicks[__instance.thingIDNumber] = now;
                return false;
            }

            // Запись не удаляем: иначе следующий тик снова начал бы отсчёт
            // и пауза вставлялась бы перед каждой пачкой, растягивая дождь
            return now - start >= delay;
        }

        // Отгремевшие спавнеры себя не убирают (Destroy мимо нас), поэтому
        // выбрасываем записи старше двух дней — дождь столько не живёт
        private static void ForgetOldEntries(int now)
        {
            if (spawnTicks.Count == 0)
                return;

            var stale = new List<int>();
            foreach (var pair in spawnTicks)
            {
                if (now - pair.Value > 120000)
                    stale.Add(pair.Key);
            }
            foreach (int id in stale)
                spawnTicks.Remove(id);
        }
    }
}
