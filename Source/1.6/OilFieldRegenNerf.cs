using HarmonyLib;
using Verse;

namespace HSKMoreHardcore
{
    // Восстановление нефтяных залежей Rimefeller.
    //
    // Поле копит нефть обратно в Rimefeller.OilField.Tick(): если с последней
    // откачки прошло больше 240 тиков, каждый тик прибавляется RegenRate, а это
    // MaxCapacity / 14400000 * множитель из настроек мода. При 100% пустая
    // залежь набирается за 240 игровых дней — то есть нефть по сути бесконечная.
    //
    // Глушим сам геттер RegenRate, не трогая настройки мода: сколько бы игрок
    // там ни выставил, действует наш множитель oilFieldRegenMultiplier
    // (0 в HardcoreSettings.xml — восстановления нет совсем).
    [StaticConstructorOnStartup]
    public static class OilFieldRegenNerf
    {
        static OilFieldRegenNerf()
        {
            var type = AccessTools.TypeByName("Rimefeller.OilField");
            var getter = type == null ? null : AccessTools.PropertyGetter(type, "RegenRate");
            if (getter == null)
                return; // Rimefeller не установлен или класс переименован

            new Harmony("linya.hskmorehardcore.oilfieldregen").Patch(getter,
                postfix: new HarmonyMethod(typeof(OilFieldRegenNerf), nameof(RegenRatePostfix)));
        }

        public static void RegenRatePostfix(ref float __result)
        {
            float mult = HardcoreSettingsDef.Instance?.oilFieldRegenMultiplier ?? 1f;
            if (mult != 1f)
                __result *= mult;
        }
    }
}
