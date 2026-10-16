using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace HSKMoreHardcore
{
    /// <summary>
    /// Увеличивает порог хорошего настроения для получения оптимиста в 10 раз (1400 -> 14000).
    /// Также увеличивает порог плохого настроения для пессимиста (-800 -> -8000).
    /// Transpiler на TraitChangerMapComponent.MapComponentTick.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class TraitMoodNerf
    {
        static TraitMoodNerf()
        {
            var harmony = new Harmony("linya.hskmorehardcore.traitmoodnerf");
            var targetType = AccessTools.TypeByName("SK.TraitChangerMapComponent");
            if (targetType == null)
                return;

            var target = AccessTools.Method(targetType, "MapComponentTick");
            if (target != null)
            {
                harmony.Patch(target,
                    transpiler: new HarmonyMethod(typeof(TraitMoodNerf), nameof(Transpiler)));
                Log.Message("[HSKMoreHardcore] TraitMoodNerf applied.");
            }
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            int patched = 0;

            for (int i = 0; i < codes.Count; i++)
            {
                // Replace 1400 (maxmoodvalue) -> 14000
                if (codes[i].opcode == OpCodes.Ldc_I4 && (int)codes[i].operand == 1400)
                {
                    codes[i].operand = 14000;
                    patched++;
                }
            }

            Log.Message($"[HSKMoreHardcore] TraitMoodNerf transpiler: patched {patched} constants.");
            return codes;
        }
    }
}
