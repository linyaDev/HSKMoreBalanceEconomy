using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace HSKMoreHardcore
{
    /// <summary>
    /// Гости Hospitality покупают решительнее: порог «понравилось — купил»
    /// в JoyGiver_BuyStuff.TryGiveJob снижен с 0.5 до 0.25 — меньше пустого
    /// разглядывания витрин. Вес шопинга среди развлечений поднят XML-патчем
    /// (Patches/Hospitality/GuestShoppingWeight.xml). JoyGiver_BuyFood
    /// наследует TryGiveJob — патч накрывает и еду.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class GuestShoppingBoost
    {
        private const float VanillaThreshold = 0.5f;
        private const float NewThreshold = 0.25f;

        static GuestShoppingBoost()
        {
            var tryGiveJob = AccessTools.Method("Hospitality.JoyGiver_BuyStuff:TryGiveJob");
            if (tryGiveJob == null)
                return; // Hospitality не установлен

            new Harmony("linya.hskmorehardcore.guestshopping").Patch(tryGiveJob,
                transpiler: new HarmonyMethod(typeof(GuestShoppingBoost), nameof(Transpiler)));
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int patched = 0;
            foreach (var ci in instructions)
            {
                if (ci.opcode == OpCodes.Ldc_R4 && ci.OperandIs(VanillaThreshold))
                {
                    ci.operand = NewThreshold;
                    patched++;
                }
                yield return ci;
            }

            if (patched == 1)
                Log.Message($"[HSKMoreHardcore] GuestShoppingBoost: порог покупки гостей {VanillaThreshold} -> {NewThreshold}.");
            else
                Log.Warning($"[HSKMoreHardcore] GuestShoppingBoost: констант {VanillaThreshold} в TryGiveJob найдено {patched} (ожидалась 1) — проверь после обновления Hospitality.");
        }
    }
}
